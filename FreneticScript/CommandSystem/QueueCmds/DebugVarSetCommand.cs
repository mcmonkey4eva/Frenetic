//
// This file is part of FreneticScript, created by Frenetic LLC.
// This code is Copyright (C) Frenetic LLC under the terms of the MIT license.
// See README.md or LICENSE.txt in the FreneticScript source root for the contents of the license.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using FreneticUtilities.FreneticExtensions;
using FreneticScript.ScriptSystems;
using FreneticScript.TagHandlers;
using FreneticScript.TagHandlers.Objects;

namespace FreneticScript.CommandSystem.QueueCmds;

/// <summary>Sets a var, for use with the compiler logic. Generally not used directly, but rather generated by the compiler for calls with a var-set style syntax.</summary>
public class DebugVarSetCommand : AbstractCommand
{
    // NOTE: Intentionally no meta!

    /// <summary>Adapts a command entry to CIL.</summary>
    /// <param name="values">The adaptation-relevant values.</param>
    /// <param name="entry">The present entry ID.</param>
    public override void AdaptToCIL(CILAdaptationValues values, int entry)
    {
        // TODO: Type verification? (Can this type be modified in the way being attempted?)
        values.MarkCommand(entry);
        CommandEntry cent = values.Entry.Entries[entry];
        bool debug = cent.DBMode.ShouldShow(DebugMode.FULL);
        string vn = cent.Arguments[0].ToString().ToLowerFast();
        string[] split = vn.Split('.');
        string mainVar = split[0];
        if (!cent.VarLookup.TryGetValue(mainVar, out SingleCILVariable locVar))
        {
            throw new ErrorInducedException("Unknown variable name '" + mainVar + "' - cannot set its value.");
        }
        TagReturnType varType = locVar.Type;
        string mode = cent.Arguments[1].ToString();
        var operationType = mode switch
        {
            "=" => ObjectOperation.SET,
            "+=" => ObjectOperation.ADD,
            "-=" => ObjectOperation.SUBTRACT,
            "*=" => ObjectOperation.MULTIPLY,
            "/=" => ObjectOperation.DIVIDE,
            _ => throw new ErrorInducedException("That setter mode (" + mode + ") does not exist!"),
        };
        if (split.Length > 1)
        {
            values.LoadLocalVariable(locVar.Index);
            if (locVar.Type.IsRaw)
            { // TODO: Work directly with raws
                values.ILGen.Emit(OpCodes.Newobj, locVar.Type.Type.RawInternalConstructor); // Handle raw translation if needed.
            }
            if (split.Length == 2)
            {
                values.ILGen.Emit(OpCodes.Dup);
            }
            values.ILGen.Emit(OpCodes.Ldstr, split[1]);
            if (varType.Type.Operation_GetSubSettable.Method.GetParameters().Length == 3)
            {
                values.LoadQueue();
                values.LoadEntry(entry);
                values.ILGen.Emit(OpCodes.Call, Method_GetOES);
            }
            values.ILGen.Emit(OpCodes.Call, varType.Type.Operation_GetSubSettable.Method);
            for (int i = 2; i < split.Length; i++)
            {
                if (i + 1 == split.Length)
                {
                    values.ILGen.Emit(OpCodes.Dup);
                }
                values.LoadEntry(entry);
                values.LoadQueue();
                values.ILGen.Emit(OpCodes.Ldstr, split[i]);
                values.ILGen.Emit(OpCodes.Call, Method_GetSubObject);
            }
            values.LoadArgumentObject(entry, 2);
            values.ILGen.Emit(OpCodes.Ldstr, split[^1]);
            values.LoadEntry(entry);
            values.LoadQueue();
            values.ILGen.Emit(OpCodes.Ldc_I4, (int)operationType);
            values.ILGen.Emit(OpCodes.Call, Method_OperateWithin);
        }
        else if (operationType == ObjectOperation.SET)
        {
            values.LoadRunnable();
            values.LoadArgumentObject(entry, 2);
            values.EnsureType(cent.Arguments[2], varType);
            values.ILGen.Emit(OpCodes.Stfld, locVar.Field);
        }
        else
        {
            ObjectOperationAttribute operation = varType.Type.Operations[(int)operationType] ?? throw new ErrorInducedException("Cannot use that setter mode (" + operationType + ") on a variable of type '" + varType.Type.TypeName + "'!");
            // This method: runnable.Var = runnable.Var.Operation(runnable.Entry.Arg2())
            values.LoadRunnable();
            values.LoadLocalVariable(locVar.Index);
            if (locVar.Type.IsRaw)
            { // TODO: Work directly with raws
                values.ILGen.Emit(OpCodes.Newobj, locVar.Type.Type.RawInternalConstructor); // Handle raw translation if needed.
            }
            values.LoadArgumentObject(entry, 2);
            values.EnsureType(cent.Arguments[2], new TagReturnType(varType.Type, false));
            if (operation.Method.GetParameters().Length == 3)
            {
                values.LoadQueue();
                values.LoadEntry(entry);
                values.ILGen.Emit(OpCodes.Call, Method_GetOES);
            }
            values.ILGen.Emit(OpCodes.Call, operation.Method);
            if (locVar.Type.IsRaw)
            { // TODO: Work directly with raws
                values.ILGen.Emit(OpCodes.Ldfld, locVar.Type.Type.RawInternalField); // Handle raw translation if needed.
            }
            values.ILGen.Emit(OpCodes.Stfld, locVar.Field);
        }
        if (debug) // If in debug mode...
        {
            values.LoadLocalVariable(locVar.Index);
            if (locVar.Type.IsRaw)
            { // TODO: Work directly with raws
                values.ILGen.Emit(OpCodes.Newobj, locVar.Type.Type.RawInternalConstructor); // Handle raw translation if needed.
            }
            values.ILGen.Emit(OpCodes.Ldstr, vn);
            values.LoadQueue();
            values.LoadEntry(entry);
            values.ILGen.Emit(OpCodes.Call, Method_DebugHelper);
        }
    }

    /// <summary>References <see cref="GetSubObject(TemplateObject, CommandEntry, CommandQueue, string)"/>.</summary>
    public static MethodInfo Method_GetSubObject = typeof(DebugVarSetCommand).GetMethod(nameof(GetSubObject));

    /// <summary>References <see cref="SetSubObject(TemplateObject, TemplateObject, CommandEntry, CommandQueue, string)"/>.</summary>
    public static MethodInfo Method_SetSubObject = typeof(DebugVarSetCommand).GetMethod(nameof(SetSubObject));

    /// <summary>References <see cref="DebugHelper(TemplateObject, string, CommandQueue, CommandEntry)"/>.</summary>
    public static MethodInfo Method_DebugHelper = typeof(DebugVarSetCommand).GetMethod(nameof(DebugHelper));

    /// <summary>References <see cref="GetOES(CommandQueue, CommandEntry)"/>.</summary>
    public static MethodInfo Method_GetOES = typeof(DebugVarSetCommand).GetMethod(nameof(GetOES));

    /// <summary>References <see cref="OperateWithin(TemplateObject, TemplateObject, TemplateObject, string, CommandEntry, CommandQueue, ObjectOperation)"/>.</summary>
    public static MethodInfo Method_OperateWithin = typeof(DebugVarSetCommand).GetMethod(nameof(OperateWithin));

    /// <summary>Runs an operation on an object dynamically.</summary>
    /// <param name="within">The object being ran within.</param>
    /// <param name="start">The object to run on.</param>
    /// <param name="input">The value to input to the operation.</param>
    /// <param name="label">The label to set back into.</param>
    /// <param name="entry">The command entry.</param>
    /// <param name="queue">The queue.</param>
    /// <param name="operation">The operation to perform.</param>
    public static void OperateWithin(TemplateObject within, TemplateObject start, TemplateObject input, string label, CommandEntry entry, CommandQueue queue, ObjectOperation operation)
    {
        if (start is DynamicTag dynTag)
        {
            OperateWithin(within, dynTag.Internal, input, label, entry, queue, operation);
            return;
        }
        if (operation != ObjectOperation.SET)
        {
            TagType type = start.GetTagType(entry.TagSystem.Types);
            ObjectOperationAttribute opAttrib = type.Operations[(int)operation] ?? throw new ErrorInducedException("Cannot use that setter mode (" + operation + ") on a variable of type '" + type.TypeName + "'!");
            input = opAttrib.ObjectFunc(start, input, GetOES(queue, entry));
        }
        TagType withinType = within.GetTagType(entry.TagSystem.Types);
        if (withinType.Operation_Set == null)
        {
            throw new ErrorInducedException("Cannot set back into a variable of type '" + withinType.TypeName + "'!");
        }
        withinType.Operation_Set.SetFunc(within, input, label, GetOES(queue, entry));
    }

    /// <summary>References <see cref="Operate(TemplateObject, TemplateObject, CommandEntry, CommandQueue, ObjectOperation)"/>.</summary>
    public static MethodInfo Method_Operate = typeof(DebugVarSetCommand).GetMethod(nameof(Operate));

    /// <summary>Runs an operation on an object dynamically.</summary>
    /// <param name="start">The object to run on.</param>
    /// <param name="input">The value to input to the operation.</param>
    /// <param name="entry">The command entry.</param>
    /// <param name="queue">The queue.</param>
    /// <param name="operation">The operation to perform.</param>
    /// <returns>The result of the operation.</returns>
    public static TemplateObject Operate(TemplateObject start, TemplateObject input, CommandEntry entry, CommandQueue queue, ObjectOperation operation)
    {
        if (start is DynamicTag dynTag)
        {
            return Operate(dynTag.Internal, input, entry, queue, operation);
        }
        TagType type = start.GetTagType(entry.TagSystem.Types);
        ObjectOperationAttribute opAttrib = type.Operations[(int)operation] ?? throw new ErrorInducedException("Cannot use that setter mode (" + operation + ") on a variable of type '" + type.TypeName + "'!");
        return opAttrib.ObjectFunc(start, input, GetOES(queue, entry));
    }

    /// <summary>Helps set sub-objects dynamically for the var-set command.</summary>
    /// <param name="entry">The relevant command entry.</param>
    /// <param name="queue">The relevant queue.</param>
    /// <param name="value">The value to insert.</param>
    /// <param name="start">The starting object.</param>
    /// <param name="label">The sub-object label.</param>
    public static void SetSubObject(TemplateObject start, TemplateObject value, CommandEntry entry, CommandQueue queue, string label)
    {
        if (start is DynamicTag dynTag)
        {
            SetSubObject(dynTag.Internal, value, entry, queue, label);
            return;
        }
        TagType type = start.GetTagType(entry.TagSystem.Types);
        if (type.Operation_GetSubSettable == null)
        {
            throw new ErrorInducedException("Cannot get sub-objects on a variable of type '" + type.TypeName + "'!");
        }
        type.Operation_Set.SetFunc(start, value, label, GetOES(queue, entry));
    }

    /// <summary>Helps get sub-objects dynamically for the var-set command.</summary>
    /// <param name="entry">The relevant command entry.</param>
    /// <param name="queue">The relevant queue.</param>
    /// <param name="start">The starting object.</param>
    /// <param name="label">The sub-object label.</param>
    /// <returns>The sub-object gotten.</returns>
    public static TemplateObject GetSubObject(TemplateObject start, CommandEntry entry, CommandQueue queue, string label)
    {
        if (start is DynamicTag dynTag)
        {
            return GetSubObject(dynTag.Internal, entry, queue, label);
        }
        TagType type = start.GetTagType(entry.TagSystem.Types);
        if (type.Operation_GetSubSettable == null)
        {
            throw new ErrorInducedException("Cannot get sub-objects on a variable of type '" + type.TypeName + "'!");
        }
        return type.Operation_GetSubSettable.StringFunc(start, label, GetOES(queue, entry));
    }

    /// <summary>Helps debug output for the var-set command.</summary>
    /// <param name="newValue">The new variable value.</param>
    /// <param name="varName">The variable name.</param>
    /// <param name="queue">The queue.</param>
    /// <param name="entry">The entry.</param>
    public static void DebugHelper(TemplateObject newValue, string varName, CommandQueue queue, CommandEntry entry)
    {
        if (entry.ShouldShowGood(queue))
        {
            entry.GoodOutput(queue, "Updated variable '" + TextStyle.Separate + varName + TextStyle.Outgood + "' to value: " + TextStyle.Separate + newValue.GetDebugString());
        }
    }

    /// <summary>Gets an object edit source for a queue+entry pair.</summary>
    /// <param name="queue">The queue.</param>
    /// <param name="entry">The entry.</param>
    /// <returns>The edit source.</returns>
    public static ObjectEditSource GetOES(CommandQueue queue, CommandEntry entry)
    {
        return new ObjectEditSource() { Queue = queue, Entry = entry, Error = queue.Error };
    }

    /// <summary>Constructs the command.</summary>
    public DebugVarSetCommand()
    {
        Name = "\0DebugVarSet";
        Arguments = "<invalid command name>";
        Description = "Sets or modifies a variable.";
        IsDebug = true;
        IsFlow = true;
        Asyncable = true;
        MinimumArguments = 3;
        MaximumArguments = 3;
    }

    /// <summary>Executs the command.</summary>
    /// <param name="queue">The command queue involved.</param>
    /// <param name="entry">The entry to execute with.</param>
    public static void Execute(CommandQueue queue, CommandEntry entry)
    {
        queue.HandleError(entry, "This command MUST be compiled!");
    }
}
