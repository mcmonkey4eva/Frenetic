﻿using System;
using System.Collections.Generic;
using FreneticScript.TagHandlers;
using FreneticScript.TagHandlers.Objects;

namespace FreneticScript.CommandSystem.CommonCmds
{
    // <--[command]
    // @Name define
    // @Arguments <variable to set> <new value>
    // @Short Modifies the value of a specified queue variable, or creates a new one.
    // @Updated 2016/04/27
    // @Authors mcmonkey
    // @Group Queue
    // @Minimum 2
    // @Maximum 2
    // @Description
    // The define command sets a <@link explanation Queue Variables>variable<@/link> onto the queue it is running in.
    // Note that it applies onto to the current stack entry (Excluding cases where injection was used).
    // TODO: Explain more!
    // @Example
    // // This example sets variable "name" to "value"
    // define name value;
    // @Example
    // // TODO: More examples!
    // Var <Dynamic> <Dynamic> returns the value of the set definition.
    // -->
    class DefineCommand : AbstractCommand // TODO: Public!
    {
        public DefineCommand()
        {
            Name = "define";
            Arguments = "<variable to set> <new value>";
            Description = "Modifies the value of a specified queue variable, or creates a new one.";
            IsFlow = true;
            Asyncable = true;
            MinimumArguments = 2;
            MaximumArguments = 2;
            ObjectTypes = new List<Func<TemplateObject, TemplateObject>>()
            {
                TextTag.For,
                TemplateObject.Basic_For
            };
        }

        public override void Execute(CommandQueue queue, CommandEntry entry)
        {
            string target = entry.GetArgument(queue, 0);
            TemplateObject newvalue = entry.GetArgumentObject(queue, 1);
            queue.SetVariable(target, newvalue);
            if (entry.ShouldShowGood(queue))
            {
                entry.Good(queue, "Queue variable '<{text_color.emphasis}>" + TagParser.Escape(target.ToLowerFast()) +
                    "<{text_color.base}>' set to '<{text_color.emphasis}>" + TagParser.Escape(newvalue.ToString()) + "<{text_color.base}>'.");
            }
        }
    }
}
