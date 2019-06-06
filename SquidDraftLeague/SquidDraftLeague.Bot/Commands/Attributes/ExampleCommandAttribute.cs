using System;
using System.Collections.Generic;
using System.Text;

namespace SquidDraftLeague.Bot.Commands.Attributes
{
    public class ExampleCommandAttribute : Attribute
    {
        public string Example { get; }

        public ExampleCommandAttribute(string example)
        {
            this.Example = example;
        }
    }
}
