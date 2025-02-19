using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Reflection;
using System.Linq;

namespace AdvancedMERTools
{

    [Serializable]
    public class If : ActionsFunctioner
    {
        public ScriptValue Statement;

        public override void OnValidate()
        {
            Statement.OnValidate();
            Actions.ForEach(x => x.OnValidate());
        }

        public override FunctionReturn Execute(FunctionArgument args)
        {
            args.PrintDebug("IF");
            args.PrintDebug(" condition:", +1);
            if (!ConditionCheck(args, Statement)) {
                args.DebugIndent -= 1;
                args.PrintDebug(" condition is FALSE, skipping actions", -2);
                return new FunctionReturn { result = FunctionResult.FunctionCheck, value = false };
            }
            args.DebugIndent -= 1;
            return ExecuteActions(args, FunctionResult.FunctionCheck);
        }
    }

    [Serializable]
    public class ElseIf : ActionsFunctioner
    {
        public ScriptValue Statement;

        public override void OnValidate()
        {
            Statement.OnValidate();
            Actions.ForEach(x => x.OnValidate());
        }

        public override FunctionReturn Execute(FunctionArgument args)
        {
            args.PrintDebug("ELIF");
            args.PrintDebug(" condition:", +1);
            if (!ConditionCheck(args, Statement)) {
                args.DebugIndent -= 1;
                args.PrintDebug(" condition is FALSE, skipping actions", -2);
                return new FunctionReturn { result = FunctionResult.FunctionCheck, value = false };
            }
            return ExecuteActions(args, FunctionResult.FunctionCheck);
        }
    }

    [Serializable]
    public class Else : ActionsFunctioner
    {
        public override void OnValidate()
        {
            Actions.ForEach(x => x.OnValidate());
        }

        public override FunctionReturn Execute(FunctionArgument args)
        {
            args.PrintDebug("ELSE");
            return ExecuteActions(args);
        }
    }

    [Serializable]
    public class While : ActionsFunctioner
    {
        public ScriptValue Condition;

        public override void OnValidate()
        {
            Condition.OnValidate();
            Actions.ForEach(x => x.OnValidate());
        }

        public override FunctionReturn Execute(FunctionArgument args)
        {
            while (true)
            {
                if (!ConditionCheck(args, Condition))
                    return new FunctionReturn();
                FunctionReturn result = ExecuteActions(args);
                switch (result.result)
                {
                    case FunctionResult.Break:
                        return new FunctionReturn();
                    case FunctionResult.Return:
                        return result;
                }
            }
        }
    }

    [Serializable]
    public class For : ActionsFunctioner
    {
        public ScriptValue RepeatCount;

        public override void OnValidate()
        {
            RepeatCount.OnValidate();
            Actions.ForEach(x => x.OnValidate());
        }

        public override FunctionReturn Execute(FunctionArgument args)
        {
            int n = RepeatCount.GetValue(args, 1);
            for (int i = 0; i < n; i++)
            {
                FunctionReturn result = ExecuteActions(args);
                switch (result.result)
                {
                    case FunctionResult.Break:
                        return new FunctionReturn();
                    case FunctionResult.Return:
                        return result;
                }
            }
            return new FunctionReturn();
        }
    }

    [Serializable]
    public class ForEach : ActionsFunctioner
    {
        public ScriptValue Array;
        public string ControlVariable;

        public override void OnValidate()
        {
            Array.OnValidate();
            Actions.ForEach(x => x.OnValidate());
        }

        public override FunctionReturn Execute(FunctionArgument args)
        {
            object obj = Array.GetValue(args);
            if (obj == null || !(obj is object[]))
                return new FunctionReturn();
            object[] arr = (object[])obj;
            for (int i = 0; i < arr.Length; i++)
            {
                args.Function.FunctionVariables[ControlVariable] = arr[i];
                FunctionReturn result = ExecuteActions(args);
                switch (result.result)
                {
                    case FunctionResult.Break:
                        return new FunctionReturn();
                    case FunctionResult.Return:
                        return result;
                }
            }
            return new FunctionReturn();
        }
    }

    [Serializable]
    public class SetVariable : Function
    {
        public ScriptValue VariableName;
        public ScriptValue ValueToAssign;
        [Header("0: Function, 1: Script, 2: Schematic, 3: Game")]
        public ScriptValue AccessLevel;

        public override void OnValidate()
        {
            VariableName.OnValidate();
            ValueToAssign.OnValidate();
            AccessLevel.OnValidate();
        }

        public override FunctionReturn Execute(FunctionArgument args)
        {
            object obj = VariableName.GetValue(args);
            object obj2 = AccessLevel.GetValue(args);
            object v = ValueToAssign.GetValue(args);
            if (obj != null && obj is string && obj2 != null && (obj2 is int || obj2 is float))
            {
                string str = Convert.ToString(obj);
                int val = Math.Min(3, Math.Max(0, Mathf.RoundToInt(Convert.ToSingle(obj2))));
                string scope = val == 0 ? "func" : val == 1 ? "script" : val == 2 ? "schem" : "game";
                args.PrintDebug($"SET_VAR[{scope}] {str}={v}");
                switch (val)
                {
                    case 0:
                        args.Function.FunctionVariables[str] = v;
                        break;
                    case 1:
                        args.Function.ScriptVariables[str] = v;
                        break;
                    case 2:
                        AdvancedMERTools.Singleton.SchematicVariables[args.schematic][str] = v;
                        break;
                    case 3:
                        AdvancedMERTools.Singleton.RoundVariable[str] = v;
                        break;
                }
            }
            return new FunctionReturn();
        }
    }

    [Serializable]
    public class Return : Function
    {
        public ScriptValue ReturnValue;

        public override void OnValidate()
        {
            ReturnValue.OnValidate();
        }

        public override FunctionReturn Execute(FunctionArgument args)
        {
            return new FunctionReturn { value = ReturnValue.GetValue(args), result = FunctionResult.Return };
        }
    }

    [Serializable]
    public class Wait : Function
    {
        public ScriptValue WaitSecond;

        public override void OnValidate()
        {
            WaitSecond.OnValidate();
        }
    }

    [Serializable]
    public class CallFunction : Function
    {
        public List<FCFEModule> FunctionModules;

        public override void OnValidate()
        {
        }

        public override FunctionReturn Execute(FunctionArgument args) {
            args.PrintDebug($"calling functions (maybe broken); count: {(FunctionModules != null ? FunctionModules.Count : -1)}");
            FCFEModule.Execute(FunctionModules, args);
            return new FunctionReturn();
        }
    }

    [Serializable]
    public class CallGroovyNoise : Function
    {
        [Header("Caution: IDs won't be updated automatically!!")]
        public List<FCGNModule> Modules;

        public override void OnValidate()
        {
        }

        public override FunctionReturn Execute(FunctionArgument args)
        {
            args.PrintDebug($"calling GroovyNoises, count: {(Modules != null ? Modules.Count : -1)}");
            FCGNModule.Execute(Modules, args);
            return new FunctionReturn();
        }
    }

    [Serializable]
    public class PlayAnimation : Function
    {
        public List<FAnimationDTO> AnimationModules;

        public override void OnValidate()
        {
        }

        public override FunctionReturn Execute(FunctionArgument args)
        {
            args.PrintDebug($"calling Animations, count: {(AnimationModules != null ? AnimationModules.Count : -1)}");
            FCGNModule.Execute(AnimationModules, args);
            return new FunctionReturn();
        }
    }

    [Serializable]
    public class SendMessage : Function
    {
        public List<FMessageModule> MessageModules;

        public override void OnValidate()
        {
        }

        public override FunctionReturn Execute(FunctionArgument args)
        {
            args.PrintDebug($"calling SendMessage, count: {(MessageModules != null ? MessageModules.Count : -1)}");
            FCGNModule.Execute(MessageModules, args);
            return new FunctionReturn();
        }
    }

    [Serializable]
    public class SendCommand : Function
    {
        public List<FCommanding> CommandModules;

        public override void OnValidate()
        {
        }

        public override FunctionReturn Execute(FunctionArgument args)
        {
            args.PrintDebug($"calling SendCommand, count: {(CommandModules != null ? CommandModules.Count : -1)}");
            FCGNModule.Execute(CommandModules, args);
            return new FunctionReturn();
        }
    }

    [Serializable]
    public class DropItems : Function
    {
        public List<FDropItem> DropItemsModules;

        public override void OnValidate()
        {
        }

        public override FunctionReturn Execute(FunctionArgument args)
        {
            args.PrintDebug($"calling DropItems, count: {(DropItemsModules != null ? DropItemsModules.Count : -1)}");
            FCGNModule.Execute(DropItemsModules, args);
            return new FunctionReturn();
        }
    }

    [Serializable]
    public class Explode : Function
    {
        public List<FExplodeModule> ExplodeModules;

        public override void OnValidate()
        {
        }

        public override FunctionReturn Execute(FunctionArgument args)
        {
            args.PrintDebug($"calling Explode, count: {(ExplodeModules != null ? ExplodeModules.Count : -1)}");
            FCGNModule.Execute(ExplodeModules, args);
            return new FunctionReturn();
        }
    }

    [Serializable]
    public class GiveEffect : Function
    {
        public List<FEffectGivingModule> EffectModules;

        public override void OnValidate()
        {
        }

        public override FunctionReturn Execute(FunctionArgument args)
        {
            args.PrintDebug($"calling EffectModules, count: {(EffectModules != null ? EffectModules.Count : -1)}");
            FCGNModule.Execute(EffectModules, args);
            return new FunctionReturn();
        }
    }

    [Serializable]
    public class PlayAudio : Function
    {
        public List<FAudioModule> AudioModules;

        public override void OnValidate()
        {
        }

        public override FunctionReturn Execute(FunctionArgument args)
        {
            args.PrintDebug($"calling AudioModules, count: {(AudioModules != null ? AudioModules.Count : -1)}");
            FCGNModule.Execute(AudioModules, args);
            return new FunctionReturn();
        }
    }
}
