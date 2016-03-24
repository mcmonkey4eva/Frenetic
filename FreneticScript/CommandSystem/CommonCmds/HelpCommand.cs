﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FreneticScript.TagHandlers;
using FreneticScript.TagHandlers.Objects;

namespace FreneticScript.CommandSystem.CommonCmds
{
    class HelpCommand : AbstractCommand
    {
        public HelpCommand()
        {
            Name = "help";
            Description = "Shows help information on any command.";
            Arguments = "<command name>";
            MinimumArguments = 1;
            MaximumArguments = 1;
            ObjectTypes = new List<Func<TemplateObject, TemplateObject>>()
            {
                (input) =>
                {
                    return new TextTag(input.ToString());
                }
            };
        }

        public override void Execute(CommandEntry entry)
        {
            string cmd = entry.GetArgument(0);
            AbstractCommand acmd;
            if (!entry.Command.CommandSystem.RegisteredCommands.TryGetValue(cmd, out acmd))
            {
                entry.Error("Unrecognized command name!");
                return;
            }
            acmd.ShowUsage(entry, false);
        }
    }
}
