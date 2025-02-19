﻿using System;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.IO;
using UnityEditor;
using UnityEngine;
using System.Reflection;
using Exiled.API.Features;
using MapEditorReborn.API.Features.Objects;
using System.Linq;

namespace AdvancedMERTools
{

    public class FunctionExecutor : AMERTInteractable
    {
        void Start()
        {
            //if (!AdvancedMERTools.Singleton.FunctionExecutors.ContainsKey(OSchematic))
            //    AdvancedMERTools.Singleton.FunctionExecutors.Add(OSchematic, new Dictionary<string, FunctionExecutor> { });
            if (AdvancedMERTools.Singleton.FunctionExecutors[OSchematic].ContainsKey(data.FunctionName))
            {
                ServerConsole.AddLog($"WARNING! There's another function named: {data.FunctionName}. Overlapped Function Name is not allowed!", ConsoleColor.Red);
                Destroy(this);
                return;
            }
            AdvancedMERTools.Singleton.FunctionExecutors[OSchematic].Add(data.FunctionName, this);
            data.OSchematic = OSchematic;
        }

        public FEDTO data;
    }

    public class FunctionArgument
    {
        public FunctionArgument()
        {
        }

        public FunctionArgument(AMERTInteractable interactable, Player player = null)
        {
            _schematic = interactable.OSchematic;
            //transform = interactable.transform;
            this.player = player;
        }

        public List<object> Arguments;
        public FEDTO Function;
        public Player player;
        public SchematicObject _schematic;
        public SchematicObject schematic => _schematic ?? Function?.OSchematic;
        public Transform transform => schematic?.transform;
        public List<(object, int)> Levels;

        public int DebugIndent = 0;
    }

    public class FunctionReturn
    {
        public object value;
        public FunctionResult result;
    }

    public enum FunctionResult
    {
        Default,
        FunctionCheck,
        Return,
        Continue,
        Break,
    }

    [Serializable]
    public class FEDTO : ActionsFunctioner
    {
        public override FunctionReturn Execute(FunctionArgument args)
        {
            //ServerConsole.AddLog("!!!");
            FunctionVariables.Clear();
            args.Function = this;
            args.PrintDebug($"FUNC ({FunctionName})");
            if (ArgumentsName != null)
                args.PrintDebug(" argument names: " + string.Join(", ", ArgumentsName));
            if (args.Arguments != null)
                args.PrintDebug(" argument values: " + string.Join(", ", args.Arguments.Select(arg => $"({arg})[{arg.GetType().Name}]")));
            args.PrintDebug(" condition:", +1);
            if (!ConditionCheck(args, Conditions)) {
                args.DebugIndent -= 1;
                args.PrintDebug(" condition is FALSE, skipping actions", -2);
                return new FunctionReturn { value = null };
            }
            args.DebugIndent -= 1;
            return ExecuteActions(args, FunctionResult.Return);
        }

        public string FunctionName;
        public string[] ArgumentsName;
        public SchematicObject OSchematic;
        public List<ScriptValue> Conditions;
        public Dictionary<string, object> FunctionVariables = new Dictionary<string, object> { };
        public Dictionary<string, object> ScriptVariables = new Dictionary<string, object> { };
    }

    [Serializable]
    public class ScriptAction
    {
        public FunctionType ActionType;
        public Function function;

        public FunctionReturn Execute(FunctionArgument args)
        {
            if (function == null)
                return null;
            if (!EnumToFunc.TryGetValue(ActionType, out _))
            {
                switch (ActionType)
                {
                    case FunctionType.Break:
                        return new FunctionReturn { result = FunctionResult.Break };
                    case FunctionType.Continue:
                        return new FunctionReturn { result = FunctionResult.Continue };
                }
                return new FunctionReturn { result = FunctionResult.FunctionCheck };
            }
            return function.Execute(args);
        }

        public void OnValidate()
        {
        }

        public static readonly Dictionary<FunctionType, Type> EnumToFunc = new Dictionary<FunctionType, Type>
    {
        { FunctionType.If, typeof(If) },
        { FunctionType.ElseIf, typeof(ElseIf) },
        { FunctionType.Else, typeof(Else) },
        { FunctionType.While, typeof(While) },
        { FunctionType.For, typeof(For) },
        { FunctionType.ForEach, typeof(ForEach) },
        { FunctionType.SetVariable, typeof(SetVariable) },
        { FunctionType.Return, typeof(Return) },
        { FunctionType.Wait, typeof(Wait) },
        { FunctionType.CallFunction, typeof(CallFunction) },
        { FunctionType.CallGroovyNoise, typeof(CallGroovyNoise) },
        { FunctionType.PlayAnimation, typeof(PlayAnimation) },
        { FunctionType.SendMessage, typeof(SendMessage) },
        { FunctionType.SendCommand, typeof(SendCommand) },
        { FunctionType.DropItems, typeof(DropItems) },
        { FunctionType.Explode, typeof(Explode) },
        { FunctionType.GiveEffect, typeof(GiveEffect) },
        { FunctionType.PlayAudio, typeof(PlayAudio) },
    };
    }

    [Serializable]
    public enum FunctionType
    {
        If,
        ElseIf,
        Else,
        While,
        For,
        ForEach,
        Break,
        Continue,
        SetVariable,
        Return,
        Wait,
        CallFunction,
        CallGroovyNoise,
        PlayAnimation,
        SendMessage,
        SendCommand,
        DropItems,
        Explode,
        GiveEffect,
        PlayAudio,
    }

    [Serializable]
    public class Function
    {
        public virtual void OnValidate() { }
        public virtual FunctionReturn Execute(FunctionArgument args)
        {
            return new FunctionReturn();
        }
    }

    [Serializable]
    public class DFunction : Function
    {
        public override void OnValidate()
        {
        }
    }

    [Serializable]
    public class ActionsFunctioner : Function
    {
        public List<ScriptAction> Actions;

        public override void OnValidate()
        {
            Actions.ForEach(x => x.OnValidate());
        }

        protected bool ConditionCheck(FunctionArgument args, object obj)
        {
            if (obj is ScriptValue)
            {
                object obj2 = ((ScriptValue)obj).GetValue(args);
                return obj2 != null && obj2 is bool && Convert.ToBoolean(obj2);
            }
            else if (obj is List<ScriptValue>)
            {
                return ((List<ScriptValue>)obj).TrueForAll(x =>
                {
                    object obj2 = x.GetValue(args);
                    if (obj2 == null || !(obj2 is bool))
                        return false;
                    return Convert.ToBoolean(obj2);
                });
            }
            return false;
        }

        protected FunctionReturn ExecuteActions(FunctionArgument args, FunctionResult result = FunctionResult.Default)
        {
            args.PrintDebug(" condition is TRUE, executing actions:", +1);
            bool IfActed = false;
            for (int i = 0; i < Actions.Count; i++)
            {
                args.PrintDebug($"executing {Actions[i].ActionType} action");
                if (Actions[i].ActionType == FunctionType.ElseIf || Actions[i].ActionType == FunctionType.Else)
                {
                    if (IfActed)
                        continue;
                }
                else
                    IfActed = false;
                FunctionReturn v = Actions[i].Execute(args);
                switch (v.result)
                {
                    case FunctionResult.Break:
                    case FunctionResult.Continue:
                    case FunctionResult.Return:
                        args.DebugIndent -= 2;
                        return v;
                }
                if (v.result == FunctionResult.FunctionCheck)
                {
                    switch (Actions[i].ActionType)
                    {
                        case FunctionType.If:
                        case FunctionType.ElseIf:
                            IfActed = Convert.ToBoolean(v.value);
                            break;
                    }
                }
            }
            args.DebugIndent -= 2;
            return new FunctionReturn { result = result, value = true };
        }
    }

    [Serializable]
    public class ScriptValue
    {
        public ValueType ValueType;
        public Value value;

        public object GetValue(FunctionArgument args)
        {
            if (value == null)
                return null;
            if (!EnumToV.TryGetValue(ValueType, out Type t))
                return ValueType;
            //if (t != value.GetType())
            //{
            //    value = 
            //}
            return value.GetValue(args);
        }
        public T GetValue<T>(FunctionArgument args, T def)
        {
            object obj = GetValue(args);
            if (obj == null)
                return def;
            //ServerConsole.AddLog("!");
            if ((typeof(int) == typeof(T) || typeof(float) == typeof(T)) && (obj is int || obj is float))
            {
                if (typeof(int) == typeof(T))
                    return (T)(object)(obj is int ? Convert.ToInt32(obj) : Mathf.RoundToInt(Convert.ToSingle(obj)));
                else
                    return (T)(object)Convert.ToSingle(obj);
            }
            if (obj is T)
                return (T)obj;
            return def;
        }

        public void OnValidate()
        {
        }

        public static readonly Dictionary<ValueType, Type> EnumToV = new Dictionary<ValueType, Type>
    {
        { ValueType.Integer, typeof(Integer) },
        { ValueType.Real, typeof(Real) },
        { ValueType.Bool, typeof(Bool) },
        { ValueType.String, typeof(String) },
        { ValueType.Compare, typeof(Compare) },
        { ValueType.IfThenElse, typeof(IfThenElse) },
        { ValueType.Array, typeof(Array) },
        { ValueType.Variable, typeof(Variable) },
        { ValueType.Argument, typeof(Argument) },
        { ValueType.Function, typeof(VFunction) },
        { ValueType.Vector, typeof(Vector) },
        { ValueType.NumUnaryOp, typeof(NumUnaryOp) },
        { ValueType.NumBinomialOp, typeof(NumBinomialOp) },
        { ValueType.ArrUnaryOp, typeof(ArrUnaryOp) },
        { ValueType.ArrBinomialOp, typeof(ArrBinomialOp) },
        { ValueType.VecUnaryOp, typeof(VecUnaryOp) },
        { ValueType.VecBinomialOp, typeof(VecBinomialOp) },
        { ValueType.StrUnaryOp, typeof(StrUnaryOp) },
        { ValueType.StrBinomialOp, typeof(StrBinomialOp) },
        { ValueType.ArrayEvaluateHelper, typeof(ArrayEvaluateHelper) },
        { ValueType.ConstValue, typeof(ConstValue) },
        { ValueType.EvaluateOnce, typeof(EvaluateOnce) },
        { ValueType.CollisionType, typeof(VCollisionType) },
        { ValueType.CollisionDetectTarget, typeof(CollisionDetectTarget) },
        { ValueType.EffectType, typeof(VEffectType) },
        { ValueType.EffectActionType, typeof(EffectActionType) },
        //{ ValueType.TeleportInvokeType, typeof(VTeleportInvokeType) },
        { ValueType.WarheadActionType, typeof(VWarheadActionType) },
        { ValueType.AnimationActionType, typeof(AnimationActionType) },
        { ValueType.ParameterType, typeof(VParameterType) },
        { ValueType.MessageType, typeof(VMessageType) },
        { ValueType.PlayerArray, typeof(PlayerArray) },
        { ValueType.Scp914Mode, typeof(VScp914Mode) },
        { ValueType.ItemType, typeof(VItemType) }
    };
    }

    [Serializable]
    public enum ValueType
    {
        Integer,
        Real,
        Bool,
        String,
        Compare,
        IfThenElse,
        EmptyArray,
        Array,
        Variable,
        Argument,
        Function,
        ZeroVector,
        Vector,
        NumUnaryOp,
        NumBinomialOp,
        ArrUnaryOp,
        ArrBinomialOp,
        VecUnaryOp,
        VecBinomialOp,
        StrUnaryOp,
        StrBinomialOp,
        ArrayEvaluateHelper,
        ConstValue,
        EvaluateOnce,
        CollisionType,
        CollisionDetectTarget,
        EffectType,
        EffectActionType,
        TeleportInvokeType,
        WarheadActionType,
        AnimationActionType,
        ParameterType,
        MessageType,
        PlayerArray,
        Scp914Mode,
        ItemType
    }

    [Serializable]
    public class Value
    {
        public virtual void OnValidate() { }
        public virtual object GetValue(FunctionArgument args)
        {
            //ServerConsole.AddLog("!!!!");
            return null;
        }
    }

    [Serializable]
    public class DValue : Value
    {
        public override void OnValidate()
        {
        }
    }
}
