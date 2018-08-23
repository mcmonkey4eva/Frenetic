//
// This file is created by Frenetic LLC.
// This code is Copyright (C) 2016-2017 Frenetic LLC under the terms of a strict license.
// See README.md or LICENSE.txt in the source root for the contents of the license.
// If neither of these are available, assume that neither you nor anyone other than the copyright holder
// hold any right or permission to use this software until such time as the official license is identified.
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FreneticScript.CommandSystem.Arguments;
using FreneticScript.TagHandlers;
using FreneticScript.TagHandlers.Objects;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Reflection.Emit;
using FreneticScript.CommandSystem.QueueCmds;
using FreneticScript.TagHandlers.Common;
using System.Threading;

namespace FreneticScript.CommandSystem
{
    /// <summary>
    /// Represents a series of commands, not currently being processed.
    /// </summary>
    public class CommandScript
    {
        /// <summary>
        /// Separates a string list of command inputs (separated by newlines, semicolons, ...)
        /// and returns a command script object containing all the input commands.
        /// </summary>
        /// <param name="name">The name of the script.</param>
        /// <param name="commands">The command string to parse.</param>
        /// <param name="system">The command system to create the script within.</param>
        /// <returns>A list of command strings.</returns>
        public static CommandScript SeparateCommands(string name, string commands, Commands system)
        {
            try
            {
                List<string> CommandList = new List<string>();
                List<int> Lines = new List<int>();
                int start = 0;
                bool quoted = false;
                bool qtype = false;
                int line = 0;
                for (int i = 0; i < commands.Length; i++)
                {
                    if (!quoted && commands[i] == '/' && i + 1 < commands.Length && commands[i + 1] == '/')
                    {
                        int x = i;
                        while (x < commands.Length && commands[x] != '\n')
                        {
                            x++;
                        }
                        if (x < commands.Length)
                        {
                            commands = commands.Substring(0, i) + commands.Substring(x);
                        }
                        else
                        {
                            break;
                        }
                    }
                    else if (!quoted && commands[i] == '/' && i + 1 < commands.Length && commands[i + 1] == '*')
                    {
                        int x;
                        for (x = i; x < commands.Length && !(commands[x] == '*' && x + 1 < commands.Length && commands[x + 1] == '/'); x++)
                        {
                            if (commands[x] == '\n')
                            {
                                line++;
                            }
                        }
                        if (x + 1 < commands.Length)
                        {
                            commands = commands.Substring(0, i) + commands.Substring(x + 1);
                        }
                        else
                        {
                            break;
                        }
                    }
                    else if (commands[i] == '"' && (!quoted || qtype))
                    {
                        qtype = true;
                        quoted = !quoted;
                    }
                    else if (commands[i] == '\'' && (!quoted || !qtype))
                    {
                        qtype = false;
                        quoted = !quoted;
                    }
                    else if (!quoted && commands[i] == ';')
                    {
                        if (start < i)
                        {
                            Lines.Add(line);
                            CommandList.Add(commands.Substring(start, i - start).Trim());
                        }
                        start = i + 1;
                    }
                    else if (((commands[i] == '{' && (i == 0 || commands[i - 1] != '<')) || (commands[i] == '}' && (i + 1 >= commands.Length || commands[i + 1] != '>'))) && !quoted)
                    {
                        if (start < i)
                        {
                            Lines.Add(line);
                            CommandList.Add(commands.Substring(start, i - start).Trim());
                        }
                        Lines.Add(line);
                        CommandList.Add(commands[i].ToString());
                        start = i + 1;
                        continue;
                    }
                    if (commands[i] == '\n')
                    {
                        line++;
                    }
                }
                if (start < commands.Length)
                {
                    Lines.Add(line);
                    CommandList.Add(commands.Substring(start).Trim());
                }
                return new CommandScript(name, CreateBlock(name, Lines, CommandList, null, system, "", 0, out bool herr), 0);
            }
            catch (Exception ex)
            {
                if (ex is ErrorInducedException)
                {
                    system.Context.BadOutput("Error parsing script: " + ex.Message);
                }
                else
                {
                    system.Context.BadOutput("Exception parsing script: " + ex.ToString());
                }
                return null;
            }
        }

        /// <summary>
        /// Converts a list of command strings to a CommandEntry list, handling any { braced } blocks inside.
        /// </summary>
        /// <param name="name">The name of the script.</param>
        /// <param name="lines">The file line numbers for the corresponding command strings.</param>
        /// <param name="from">The command strings.</param>
        /// <param name="entry">The entry that owns this block.</param>
        /// <param name="system">The command system to create this block inside.</param>
        /// <param name="tabs">How far out tabulation should go.</param>
        /// <param name="had_error">Whether there was a compile error.</param>
        /// <param name="istart">The starting index.</param>
        /// <returns>A list of entries with blocks separated.</returns>
        public static List<CommandEntry> CreateBlock(string name, List<int> lines, List<string> from, CommandEntry entry, Commands system, string tabs, int istart, out bool had_error)
        {
            List<CommandEntry> toret = new List<CommandEntry>();
            List<string> Temp = null;
            List<int> Temp2 = null;
            int blocks = 0;
            for (int i = 0; i < from.Count; i++)
            {
                if (from[i] == "{")
                {
                    blocks++;
                    if (blocks == 1)
                    {
                        Temp = new List<string>();
                        Temp2 = new List<int>();
                    }
                    else
                    {
                        Temp.Add("{");
                        Temp2.Add(lines[i]);
                    }
                }
                else if (from[i] == "}")
                {
                    blocks--;
                    if (blocks == 0)
                    {
                        if (toret.Count == 0)
                        {
                            List<CommandEntry> block = CreateBlock(name, Temp2, Temp, entry, system, tabs + "    ", istart, out bool err);
                            if (err)
                            {
                                had_error = true;
                                return block;
                            }
                            toret.AddRange(block);
                            istart += block.Count;
                        }
                        else
                        {
                            CommandEntry cent = toret[toret.Count - 1];
                            List<CommandEntry> block = CreateBlock(name, Temp2, Temp, cent, system, tabs + "    ", istart, out bool err);
                            if (err)
                            {
                                had_error = true;
                                return block;
                            }
                            cent.BlockStart = istart;
                            istart += block.Count;
                            cent.BlockEnd = istart - 1;
                            List<CommandEntry> toinj = new List<CommandEntry>(block);
                            int bc = block.Count;
                            if (cent.Command != null)
                            {
                                cent.Command.AdaptBlockFollowers(cent, toinj, block);
                            }
                            istart += (toinj.Count - bc);
                            cent.InnerCommandBlock = block;
                            toret.AddRange(toinj);
                        }
                    }
                    else if (blocks < 0)
                    {
                        blocks = 0;
                    }
                    else
                    {
                        Temp.Add("}");
                        Temp2.Add(lines[i]);
                    }
                }
                else if (blocks > 0)
                {
                    Temp.Add(from[i]);
                    Temp2.Add(lines[i]);
                }
                else
                {
                    CommandEntry centry = CommandEntry.FromInput(from[i], system, name, lines[i], tabs);
                    if (centry != null)
                    {
                        istart++;
                        toret.Add(centry);
                    }
                }
            }
            for (int i = 0; i < toret.Count; i++)
            {
                if (toret[i].Command != null)
                {
                    string msg = toret[i].Command.TestForValidity(toret[i]);
                    if (msg != null)
                    {
                        string fullmsg = "FAILED TO COMPILE SCRIPT '" + name + "': (line " + toret[i].ScriptLine + "): " + msg;
                        system.Context.BadOutput(fullmsg);
                        had_error = true;
                        toret.Clear();
                        // TODO: Maybe throw an exception?
                        toret.Add(CommandEntry.CreateErrorOutput(fullmsg, system, name, tabs));
                        return toret;
                    }
                }
            }
            had_error = false;
            return toret;
        }

        /// <summary>
        /// Creates a script by file name.
        /// </summary>
        /// <param name="filename">The name of the file to execute.</param>
        /// <param name="system">The command system to get the script for.</param>
        /// <returns>A command script, or null of the file does not exist.</returns>
        public static CommandScript GetByFileName(string filename, Commands system)
        {
            try
            {
                string fname = filename + ".cfg";
                return SeparateCommands(filename, system.Context.ReadTextFile(fname), system);
            }
            catch (System.IO.FileNotFoundException)
            {
                return null;
            }
            catch (Exception ex)
            {
                if (ex is ThreadAbortException)
                {
                    throw;
                }
                system.Context.BadOutput("Generating script for file '" + filename + "': " + ex.ToString());
                return null;
            }
        }

        /// <summary>
        /// The name of the script.
        /// </summary>
        public string Name;

        /// <summary>
        /// The default debugmode for this script (until set otherwise by the debug command).
        /// </summary>
        public DebugMode Debug = DebugMode.FULL;

        /// <summary>
        /// All compiled command structure set created by this script.
        /// </summary>
        public CompiledCommandStackEntry Created;

        /// <summary>
        /// Constructs a new command script.
        /// </summary>
        /// <param name="_name">The name of the script.</param>
        /// <param name="_commands">All commands in the script.</param>
        /// <param name="adj">How far to negatively adjust the entries' block positions, if any.</param>
        /// <param name="mode">What debug mode to use for this script.</param>
        public CommandScript(string _name, List<CommandEntry> _commands, int adj = 0, DebugMode mode = DebugMode.FULL)
        {
            Debug = mode;
            Name = _name.ToLowerFastFS();
            List<CommandEntry> Commands = _commands;
            Commands = new List<CommandEntry>(_commands);
            for (int i = 0; i < Commands.Count; i++)
            {
                Commands[i] = _commands[i].Duplicate();
                Commands[i].OwnIndex = i;
                Commands[i].BlockStart -= adj;
                Commands[i].BlockEnd -= adj;
            }
            Created = new CompiledCommandStackEntry()
            {
                Debug = Debug,
                Entries = Commands.ToArray()
            };
            Created.EntryData = new AbstractCommandEntryData[Created.Entries.Length];
            {
                string tname = "__script__" + IDINCR++;
                AssemblyName asmname = new AssemblyName(tname) { Name = tname };
                AssemblyBuilder asmbuild = AppDomain.CurrentDomain.DefineDynamicAssembly(asmname,
#if NET_4_5
                    AssemblyBuilderAccess.RunAndCollect
#else
                    AssemblyBuilderAccess.Run
#endif
                    );
                ModuleBuilder modbuild = asmbuild.DefineDynamicModule(tname);
                CompiledCommandStackEntry ccse = Created;
                ccse.AdaptedILPoints = new Label[ccse.Entries.Length + 1];
                TypeBuilder typebuild_c = modbuild.DefineType(tname + "__CENTRAL", TypeAttributes.Class | TypeAttributes.Public, typeof(CompiledCommandRunnable));
                MethodBuilder methodbuild_c = typebuild_c.DefineMethod("Run", MethodAttributes.Public | MethodAttributes.Virtual, typeof(void), new Type[] { typeof(CommandQueue), typeof(IntHolder), typeof(CommandEntry[]), typeof(int) });
                CILAdaptationValues.ILGeneratorTracker ilgen = new CILAdaptationValues.ILGeneratorTracker() { Internal = methodbuild_c.GetILGenerator() };
                CILAdaptationValues values = new CILAdaptationValues()
                {
                    Entry = ccse,
                    Script = this,
                    ILGen = ilgen,
                    Method = methodbuild_c,
                    DBMode = Debug
                };
                values.PushVarSet();
                for (int i = 0; i < ccse.AdaptedILPoints.Length; i++)
                {
                    ccse.AdaptedILPoints[i] = ilgen.DefineLabel();
                }
                int tagID = 0;
                TypeBuilder typebuild_c2 = modbuild.DefineType(tname + "__TAGPARSE", TypeAttributes.Class | TypeAttributes.Public);
                List<TagArgumentBit> toClean = new List<TagArgumentBit>();
                List<CILAdaptationValues.ILGeneratorTracker> ILGens = new List<CILAdaptationValues.ILGeneratorTracker>();
                for (int i = 0; i < ccse.Entries.Length; i++)
                {
                    ccse.Entries[i].DBMode = values.DBMode;
                    CILVariables[] ttvars = new CILVariables[values.LVarIDs.Count];
                    CommandEntry curEnt = ccse.Entries[i];
                    int tcounter = 0;
                    foreach (int tv in values.LVarIDs)
                    {
                        ttvars[tcounter] = values.CLVariables[tv];
                        tcounter++;
                    }
                    curEnt.CILVars = ttvars;
                    for (int a = 0; a < curEnt.Arguments.Count; a++)
                    {
                        Argument arg = curEnt.Arguments[a];
                        for (int b = 0; b < arg.Bits.Length; b++)
                        {
                            if (arg.Bits[b] is TagArgumentBit tab)
                            {
                                tagID++;
                                try
                                {
                                    ILGens.Add(GenerateTagData(typebuild_c2, ccse, tab, ref tagID, values, i, toClean));
                                }
                                catch (TagErrorInducedException ex)
                                {
                                    TagException(ccse.Entries[i], "argument " + TextStyle.Color_Separate + a + TextStyle.Color_Base, tab, ex.SubTagIndex, ex);
                                }
                                catch (ErrorInducedException ex)
                                {
                                    TagException(ccse.Entries[i], "argument " + TextStyle.Color_Separate + a + TextStyle.Color_Base, tab, 0, ex);
                                }
                            }
                        }
                    }
                    foreach (KeyValuePair<string, Argument> argPair in curEnt.NamedArguments)
                    {
                        for (int b = 0; b < argPair.Value.Bits.Length; b++)
                        {
                            if (argPair.Value.Bits[b] is TagArgumentBit tab)
                            {
                                tagID++;
                                try
                                {
                                    ILGens.Add(GenerateTagData(typebuild_c2, ccse, tab, ref tagID, values, i, toClean));
                                }
                                catch (TagErrorInducedException ex)
                                {
                                    TagException(ccse.Entries[i], "named argument '" + TextStyle.Color_Separate + argPair.Key + TextStyle.Color_Base + "'", tab, ex.SubTagIndex, ex);
                                }
                                catch (ErrorInducedException ex)
                                {
                                    TagException(ccse.Entries[i], "named argument '" + TextStyle.Color_Separate + argPair.Key + TextStyle.Color_Base + "'", tab, 0, ex);
                                }
                            }
                        }
                    }
                    bool isCallback = curEnt.Arguments.Count > 0 && curEnt.Arguments[0].ToString() == "\0CALLBACK";
                    if (!isCallback)
                    {
                        try
                        {
                            curEnt.Command.PreAdaptToCIL(values, i);
                        }
                        catch (ErrorInducedException ex)
                        {
                            throw new ErrorInducedException("On script line " + curEnt.ScriptLine + " (" + curEnt.CommandLine + "), early compile (PreAdapt) error occured: " + ex.Message);
                        }
                    }
                    CILVariables[] tvars = new CILVariables[values.LVarIDs.Count];
                    int counter = 0;
                    foreach (int tv in values.LVarIDs)
                    {
                        tvars[counter] = values.CLVariables[tv];
                        counter++;
                    }
                    curEnt.CILVars = tvars;
                    Dictionary<string, int> varlookup = new Dictionary<string, int>(tvars.Length);
                    foreach (CILVariables tv in tvars)
                    {
                        foreach (Tuple<int, string, TagType> tvt in tv.LVariables)
                        {
                            varlookup.Add(tvt.Item2, tvt.Item1);
                        }
                    }
                    curEnt.VarLookup = varlookup;
                    if (curEnt.NamedArguments.TryGetValue("\0varname", out Argument avarname))
                    {
                        if (!varlookup.ContainsKey(avarname.ToString()))
                        {
                            throw new ErrorInducedException("Error in command line " + ccse.Entries[i].ScriptLine + ": (" + ccse.Entries[i].CommandLine + "): Invalid variable save name: " + avarname.ToString());
                        }
                    }
                    if (isCallback)
                    {
                        try
                        {
                            ccse.Entries[i].Command.PreAdaptToCIL(values, i);
                        }
                        catch (ErrorInducedException ex)
                        {
                            throw new ErrorInducedException("On script line " + curEnt.ScriptLine + " (" + curEnt.CommandLine + "), early compile (PreAdapt) error occured: " + ex.Message);
                        }
                    }
                    ccse.Entries[i].DBMode = values.DBMode;
                }
                ccse.LocalVariables = new ObjectHolder[values.CLVarID];
                for (int n = 0; n < values.CLVariables.Count; n++)
                {
                    for (int x = 0; x < values.CLVariables[n].LVariables.Count; x++)
                    {
                        int ind = values.CLVariables[n].LVariables[x].Item1;
                        ccse.LocalVariables[ind] = new ObjectHolder();
                    }
                }
                ilgen.Emit(OpCodes.Ldarg, 4);
                ilgen.Emit(OpCodes.Switch, ccse.AdaptedILPoints);
                for (int i = 0; i < ccse.Entries.Length; i++)
                {
                    ilgen.MarkLabel(ccse.AdaptedILPoints[i]);
                    try
                    {
                        ccse.Entries[i].Command.AdaptToCIL(values, i);
                    }
                    catch (ErrorInducedException ex)
                    {
                        throw new ErrorInducedException("On script line " + ccse.Entries[i].ScriptLine + " (" + ccse.Entries[i].CommandLine + "), compile error (Adapt) occured: " + ex.Message);
                    }
                }
                ilgen.MarkLabel(ccse.AdaptedILPoints[ccse.AdaptedILPoints.Length - 1]);
                values.MarkCommand(ccse.Entries.Length);
                ilgen.Emit(OpCodes.Ret);
                typebuild_c.DefineMethodOverride(methodbuild_c, CompiledCommandRunnable.RunMethod);
                Type t_c = typebuild_c.CreateType();
                Type tP_c2 = typebuild_c2.CreateType();
                for (int i = 0; i < toClean.Count; i++)
                {
                    toClean[i].GetResultMethod = tP_c2.GetMethod(toClean[i].GetResultMethod.Name);
                    toClean[i].GetResultHelper = (TagArgumentBit.MethodHandler)toClean[i].GetResultMethod.CreateDelegate(typeof(TagArgumentBit.MethodHandler));
                }
                ccse.MainCompiledRunnable = Activator.CreateInstance(t_c) as CompiledCommandRunnable;
                ccse.MainCompiledRunnable.CSEntry = ccse;
#if SAVE
                StringBuilder outp = new StringBuilder();
                for (int i = 0; i < ilgen.Codes.Count; i++)
                {
                    outp.Append(ilgen.Codes[i].Key.Name + ": " + ilgen.Codes[i].Value + "\n");
                }
                for (int n = 0; n < ILGens.Count; n++)
                {
                    outp.Append("\n\n\n// -----\n\n\n");
                    for (int i = 0; i < ILGens[n].Codes.Count; i++)
                    {
                        outp.Append(ILGens[n].Codes[i].Key.Name + ": " + ILGens[n].Codes[i].Value + "\n");
                    }
                }
                System.IO.File.WriteAllText("script_" + tname + ".il", outp.ToString());
#endif
            }
        }

        /// <summary>
        /// Throws a tag failure exception.
        /// </summary>
        /// <param name="entry">Relevant command entry.</param>
        /// <param name="argumentNote">Note for the argument, like: in named argument 'fail'.</param>
        /// <param name="tab"></param>
        /// <param name="tagIndex"></param>
        /// <param name="ex"></param>
        public void TagException(CommandEntry entry, string argumentNote, TagArgumentBit tab, int tagIndex, ErrorInducedException ex)
        {
            throw new ErrorInducedException("On script line " + TextStyle.Color_Separate + entry.ScriptLine
                + TextStyle.Color_Base + " (" + TextStyle.Color_Separate + entry.CommandLine
                + TextStyle.Color_Base + "), in " + argumentNote
                + " while compiling tag " + TextStyle.Color_Separate +
                "<" + tab.HighlightString(tagIndex, TextStyle.Color_Warning) + TextStyle.Color_Separate + ">"
                + TextStyle.Color_Base + ", error occured: " + ex.Message);
        }

        /// <summary>
        /// Generates tag CIL.
        /// </summary>
        /// <param name="typeBuild_c">The type to contain this tag.</param>
        /// <param name="ccse">The CCSE available.</param>
        /// <param name="tab">The tag data.</param>
        /// <param name="tID">The ID of the tag.</param>
        /// <param name="values">The helper values.</param>
        /// <param name="entryIndex">The command entry index.</param>
        /// <param name="toClean">Cleanable tag bits.</param>
        public static CILAdaptationValues.ILGeneratorTracker GenerateTagData(TypeBuilder typeBuild_c, CompiledCommandStackEntry ccse, TagArgumentBit tab,
            ref int tID, CILAdaptationValues values, int entryIndex, List<TagArgumentBit> toClean)
        {
            int id = tID;
            List<Argument> altArgs = new List<Argument>();
            for (int sub = 0; sub < tab.Bits.Length; sub++)
            {
                if (tab.Bits[sub].Variable != null)
                {
                    altArgs.Add(tab.Bits[sub].Variable);
                }
            }
            if (tab.Fallback != null)
            {
                altArgs.Add(tab.Fallback);
            }
            for (int sx = 0; sx < altArgs.Count; sx++)
            {
                for (int b = 0; b < altArgs[sx].Bits.Length; b++)
                {
                    if (altArgs[sx].Bits[b] is TagArgumentBit)
                    {
                        tID++;
                        GenerateTagData(typeBuild_c, ccse, ((TagArgumentBit)altArgs[sx].Bits[b]), ref tID, values, entryIndex, toClean);
                    }
                }
            }
            MethodBuilder methodbuild_c = typeBuild_c.DefineMethod("TagParse_" + id, MethodAttributes.Public | MethodAttributes.Static, typeof(TemplateObject), new Type[] { typeof(TagData) });
            CILAdaptationValues.ILGeneratorTracker ilgen = new CILAdaptationValues.ILGeneratorTracker() { Internal = methodbuild_c.GetILGenerator() };
            TagType returnable = tab.Start.ResultType;
            if (returnable == null)
            {
                returnable = tab.Start.Adapt(ccse, tab, entryIndex);
            }
            if (returnable == null)
            {
                throw new TagErrorInducedException("Invalid tag top-handler '"
                    + TextStyle.Color_Separate + tab.Start.Name
                    + TextStyle.Color_Base + "' (failed to identify return type)!", 0);
            }
            TagType prevType = returnable;
            for (int x = 1; x < tab.Bits.Length; x++)
            {
                string key = tab.Bits[x].Key;
                if (!returnable.TagHelpers.ContainsKey(key))
                {
                    while (returnable.SubType != null)
                    {
                        returnable = returnable.SubType;
                        if (returnable.TagHelpers.ContainsKey(key))
                        {
                            goto ready;
                        }
                    }
                    throw new TagErrorInducedException("Invalid sub-tag '"
                        + TextStyle.Color_Separate + key + TextStyle.Color_Base + "' at sub-tag index "
                        + TextStyle.Color_Separate + x + TextStyle.Color_Base + " for type '"
                        + TextStyle.Color_Separate + prevType.TypeName + TextStyle.Color_Base
                        + (key.Trim().Length == 0 ? "' (stray '.' dot symbol?)!" : "' (sub-tag doesn't seem to exist)!"), x);
                }
                ready:
                TagHelpInfo tsh = returnable.TagHelpers[key];
                tab.Bits[x].TagHandler = tsh;
                if (tsh.Meta.ReturnTypeResult == null)
                {
                    if (tab.Bits[x].TagHandler.Meta.SpecialTypeHelper != null)
                    {
                        returnable = tab.Bits[x].TagHandler.Meta.SpecialTypeHelper(tab, x);
                    }
                    else
                    {
                        throw new TagErrorInducedException("Invalid tag ReturnType '" + TextStyle.Color_Separate + tsh.Meta.ReturnType
                            + TextStyle.Color_Base + " for tag '" + TextStyle.Color_Separate + tsh.Meta.ActualType.TypeName + "."
                            + TextStyle.Color_Separate + tsh.Meta.Name + TextStyle.Color_Base + "', cannot process properly!", x);
                    }
                }
                else
                {
                    returnable = tsh.Meta.ReturnTypeResult;
                }
            }
            int vxLen = tab.Bits.Length;
            Argument[] varBits = new Argument[vxLen];
            for (int vxi = 0; vxi < vxLen; vxi++)
            {
                varBits[vxi] = tab.Bits[vxi].Variable;
            }
            CommandEntry relevantEntry = ccse.Entries[entryIndex];
            tab.Data = new TagData()
            {
                BaseColor = TextStyle.Color_Simple,
                cInd = 0,
                CSE = ccse,
                Error = null,
                Fallback = tab.Fallback,
                Bits = tab.Bits,
                Variables = varBits,
                DBMode = relevantEntry.DBMode,
                Remaining = tab.Bits.Length,
                Start = tab.Start,
                TagSystem = tab.CommandSystem.TagSystem
            };
            if (tab.Start.Method_HandleOneObjective != null) // If objective tag handling...
            {
                ilgen.Emit(OpCodes.Ldarg_0); // Load argument: TagData.
                ilgen.Emit(OpCodes.Ldfld, TagData.Field_Start); // Load field TagData -> Start.
            }
            ilgen.Emit(OpCodes.Ldarg_0); // Load argument: TagData.
            if (tab.Start is LvarTagBase) // If the 'var' compiled tag...
            {
                int index = (int)((tab.Bits[0].Variable.Bits[0] as TextArgumentBit).InputValue as IntegerTag).Internal;
                ilgen.Emit(OpCodes.Ldc_I4, index); // Load the correct variable location.
                ilgen.Emit(OpCodes.Call, LvarTagBase.Method_HandleOneFast); // Handle it quickly and directly.
            }
            else if (tab.Start.Method_HandleOneObjective != null) // If objective tag handling...
            {
                ilgen.Emit(OpCodes.Call, tab.Start.Method_HandleOneObjective); // Run instance method: TemplateTagBase -> HandleOneObjective.
            }
            else // If faster static tag handling
            {
                ilgen.Emit(OpCodes.Call, tab.Start.Method_HandleOne); // Run static method: TemplateTagBase -> HandleOne.
            }
            int need_shrink = 0;
            for (int x = 1; x < tab.Bits.Length; x++)
            {
                TagType modt = tab.Bits[x].TagHandler.Meta.ModifierType;
                need_shrink++;
                if (modt == null) // If no direct modifier input is required, just a generic TagData input...
                {
                    ilgen.Emit(OpCodes.Ldarg_0); // Load argument: TagData.
                    if (need_shrink > 1) // If we need to shrink several at once...
                    {
                        ilgen.Emit(OpCodes.Ldc_I4, need_shrink); // Load the number of times a shrink is needed.
                        ilgen.Emit(OpCodes.Call, TagData.Method_ShrinkMulti); // Run method: TagData -> ShrinkMulti.
                    }
                    else
                    {
                        ilgen.Emit(OpCodes.Call, TagData.Method_Shrink); // Run method: TagData -> Shrink.
                    }
                    need_shrink = 0;
                }
                // If we're running a specially compiled tag...
                if (tab.Bits[x].TagHandler.Meta.SpecialCompiler)
                {
                    prevType = tab.Bits[x].TagHandler.Meta.SpecialCompileAction(ilgen, tab, x, prevType);
                }
                else // For normal tags...
                {
                    while (tab.Bits[x].TagHandler.Meta.TagType != prevType.TypeName)
                    {
                        ilgen.Emit(OpCodes.Call, prevType.GetNextTypeDown.Method);
                        prevType = prevType.SubType;
                        if (prevType == null)
                        {
                            throw new Exception("Failed to parse down a tag: type reached the base type without finding the expected tag type! (Compiler bug?)"
                                + " Processing tag " + tab + " on bit " + x);
                        }
                    }
                    prevType = tab.Bits[x].TagHandler.Meta.ReturnTypeResult;
                    if (modt != null) // If we have a modifier input type pre-requirement...
                    {
                        ilgen.Emit(OpCodes.Ldarg_0); // Load argument: TagData.
                        ilgen.Emit(OpCodes.Ldc_I4, x); // Load the correct tag modifier location in exact.
                        ilgen.Emit(OpCodes.Call, TagData.Method_GetModiferObjectKnown); // Call the method to get the tag modifier object at the x location.
                        TagType atype = tab.Bits[x].Variable.ReturnType(values);
                        if (modt != atype) // If the modifier input is of the wrong type...
                        {
                            ilgen.Emit(OpCodes.Ldarg_0); // Load argument: TagData.
                            ilgen.Emit(OpCodes.Call, modt.CreatorMethod); // Run the creator method to convert the tag to the correct type.
                        }
                    }
                    else
                    {
                        ilgen.Emit(OpCodes.Ldarg_0); // Load argument: TagData.
                    }
                    ilgen.Emit(OpCodes.Call, tab.Bits[x].TagHandler.Method); // Run the tag's own runner method.
                }
            }
            if (ccse.Debug <= DebugMode.FULL) // If debug mode is on...
            {
                ilgen.Emit(OpCodes.Ldarg_0); // Load argument: TagData.
                ilgen.Emit(OpCodes.Call, TagParser.Method_DebugTagHelper); // Debug the tag as a final step. Will give back the object to the stack.
            }
            ilgen.Emit(OpCodes.Ret); // Return.
#if NET_4_5
            methodbuild_c.SetCustomAttribute(new CustomAttributeBuilder(Ctor_MethodImplAttribute_Options, Input_Params_AggrInline));
#endif
            tab.GetResultMethod = methodbuild_c;
            toClean.Add(tab);
            return ilgen;
        }

#if NET_4_5
        /// <summary>
        /// The <see cref="MethodImplAttribute(MethodImplOptions)"/> constructor.
        /// </summary>
        public static readonly ConstructorInfo Ctor_MethodImplAttribute_Options = typeof(MethodImplAttribute).GetConstructor(new Type[] { typeof(MethodImplOptions) });

        /// <summary>
        /// An input object array reusable that contains <see cref="MethodImplOptions.AggressiveInlining"/>.
        /// </summary>
        public static readonly object[] Input_Params_AggrInline = new object[] { MethodImplOptions.AggressiveInlining };
#endif

        /// <summary>
        /// The method <see cref="Func{TagData, TemplateObject, TemplateObject}.Invoke(TagData, TemplateObject)"/> with typeparams <see cref="TagData"/>, <see cref="TemplateObject"/>, <see cref="TemplateObject"/>.
        /// </summary>
        public static readonly MethodInfo Method_Func_TD_TO_TO_Invoke = typeof(Func<TagData, TemplateObject, TemplateObject>).GetMethod(nameof(Func<TagData, TemplateObject, TemplateObject>.Invoke));

        static long IDINCR = 0;
        
        /// <summary>
        /// Creates a new queue for this script.
        /// </summary>
        /// <param name="system">The command system to make the queue in.</param>
        /// <returns>The created queue.</returns>
        public CommandQueue ToQueue(Commands system)
        {
            CommandQueue queue = new CommandQueue(this, system);
            if (Created == null)
            {
                throw new Exception("Invalid CREATED object in a CommandScript somehow?!");
            }
            queue.CommandStack.Push(Created.Duplicate());
            return queue;
        }

        /// <summary>
        /// Returns the name of the script.
        /// </summary>
        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// Gets the full command string that represents this script.
        /// </summary>
        /// <param name="tabulation">How much space to include in front of the commands.</param>
        /// <returns>The full command string.</returns>
        public string FullString(string tabulation = "")
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < Created.Entries.Length; i++)
            {
                if (!Created.Entries[i].CommandLine.Contains('\0'))
                {
                    sb.Append(Created.Entries[i].FullString());
                    if (Created.Entries[i].InnerCommandBlock != null)
                    {
                        i = Created.Entries[i].BlockEnd;
                    }
                }
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Abstract class for compiled runnables.
    /// </summary>
    public abstract class CompiledCommandRunnable
    {
        /// <summary>
        /// The command stack entry that forms this runnable.
        /// </summary>
        public CommandStackEntry CSEntry;
        
        /// <summary>
        /// This class's "Run(queue)" method.
        /// </summary>
        public static readonly MethodInfo RunMethod = typeof(CompiledCommandRunnable).GetMethod(nameof(CompiledCommandRunnable.Run), new Type[] { typeof(CommandQueue), typeof(IntHolder), typeof(CommandEntry[]), typeof(int) });

        /// <summary>
        /// Runs the runnable.
        /// </summary>
        /// <param name="queue">The queue to run on.</param>
        /// <param name="counter">The current command index.</param>
        /// <param name="fent">The first entry (the entry to start calculating at).</param>
        /// <param name="entries">The entry set ran with.</param>
        public abstract void Run(CommandQueue queue, IntHolder counter, CommandEntry[] entries, int fent);
    }

    /// <summary>
    /// Holds a 32-bit integer.
    /// </summary>
    public class IntHolder
    {
        /// <summary>
        /// The actual integer.
        /// </summary>
        public int Internal;
    }
}
