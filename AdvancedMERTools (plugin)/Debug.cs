using Exiled.API.Features;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdvancedMERTools {
    public static class Debug {
        public static void PrintDebug(this FunctionArgument args, string message, int addToIndent = 0) {
            Log.Debug($"[FE] {Indents(args.DebugIndent)}{message}");
            args.DebugIndent += addToIndent;
        }

        public static string Indents(int indent = 0) {
            string indentText = string.Empty;
            for (int i = 0; i < indent; i++)
                indentText += "  ";
            return indentText;
        }
    }
}
