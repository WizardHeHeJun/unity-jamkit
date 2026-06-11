using System;
using System.Collections.Generic;

namespace GameplayKit
{
    /// <summary>条件求值时的变量读取接口。可接到游戏自己的存档系统上。</summary>
    public interface IBlackboardReader
    {
        bool TryGetBool(string name, out bool value);
        bool TryGetInt(string name, out int value);
        bool TryGetFloat(string name, out float value);
        bool TryGetString(string name, out string value);
    }

    /// <summary>黑板快照，可直接 JsonUtility 序列化（随存档保存）。</summary>
    [Serializable]
    public class BlackboardState
    {
        [Serializable]
        public class BoolEntry { public string name; public bool value; }

        [Serializable]
        public class IntEntry { public string name; public int value; }

        [Serializable]
        public class FloatEntry { public string name; public float value; }

        [Serializable]
        public class StringEntry { public string name; public string value; }

        public List<BoolEntry> bools = new List<BoolEntry>();
        public List<IntEntry> ints = new List<IntEntry>();
        public List<FloatEntry> floats = new List<FloatEntry>();
        public List<StringEntry> strings = new List<StringEntry>();
    }

    /// <summary>
    /// 运行时黑板：内存变量表 + 变更通知。
    /// 用 <see cref="CreateFrom"/> 从黑板资产创建（自动填默认值），
    /// 值变化时触发 <see cref="OnValueChanged"/>，任务系统等据此重新评估挂起条件。
    /// </summary>
    public class Blackboard : IBlackboardReader
    {
        readonly Dictionary<string, bool> bools = new Dictionary<string, bool>();
        readonly Dictionary<string, int> ints = new Dictionary<string, int>();
        readonly Dictionary<string, float> floats = new Dictionary<string, float>();
        readonly Dictionary<string, string> strings = new Dictionary<string, string>();

        /// <summary>任意变量值变化时触发，参数为变量名。</summary>
        public event Action<string> OnValueChanged;

        public static Blackboard CreateFrom(BlackboardAsset asset)
        {
            var board = new Blackboard();
            if (asset == null) return board;

            foreach (var def in asset.variables)
            {
                if (def == null || string.IsNullOrEmpty(def.name)) continue;
                switch (def.type)
                {
                    case BlackboardVariableType.Bool: board.bools[def.name] = def.defaultBool; break;
                    case BlackboardVariableType.Int: board.ints[def.name] = def.defaultInt; break;
                    case BlackboardVariableType.Float: board.floats[def.name] = def.defaultFloat; break;
                    case BlackboardVariableType.String: board.strings[def.name] = def.defaultString ?? string.Empty; break;
                }
            }
            return board;
        }

        public void SetBool(string name, bool value)
        {
            if (bools.TryGetValue(name, out var old) && old == value) return;
            bools[name] = value;
            OnValueChanged?.Invoke(name);
        }

        public void SetInt(string name, int value)
        {
            if (ints.TryGetValue(name, out var old) && old == value) return;
            ints[name] = value;
            OnValueChanged?.Invoke(name);
        }

        public void SetFloat(string name, float value)
        {
            if (floats.TryGetValue(name, out var old) && old.Equals(value)) return;
            floats[name] = value;
            OnValueChanged?.Invoke(name);
        }

        public void SetString(string name, string value)
        {
            if (strings.TryGetValue(name, out var old) && old == value) return;
            strings[name] = value;
            OnValueChanged?.Invoke(name);
        }

        public bool TryGetBool(string name, out bool value) => bools.TryGetValue(name, out value);
        public bool TryGetInt(string name, out int value) => ints.TryGetValue(name, out value);
        public bool TryGetFloat(string name, out float value) => floats.TryGetValue(name, out value);
        public bool TryGetString(string name, out string value) => strings.TryGetValue(name, out value);

        public BlackboardState CaptureState()
        {
            var state = new BlackboardState();
            foreach (var kv in bools)
                state.bools.Add(new BlackboardState.BoolEntry { name = kv.Key, value = kv.Value });
            foreach (var kv in ints)
                state.ints.Add(new BlackboardState.IntEntry { name = kv.Key, value = kv.Value });
            foreach (var kv in floats)
                state.floats.Add(new BlackboardState.FloatEntry { name = kv.Key, value = kv.Value });
            foreach (var kv in strings)
                state.strings.Add(new BlackboardState.StringEntry { name = kv.Key, value = kv.Value });
            return state;
        }

        /// <summary>恢复快照。不触发 OnValueChanged，恢复完成后请整体重新评估。</summary>
        public void ApplyState(BlackboardState state)
        {
            bools.Clear();
            ints.Clear();
            floats.Clear();
            strings.Clear();
            if (state == null) return;
            foreach (var e in state.bools) bools[e.name] = e.value;
            foreach (var e in state.ints) ints[e.name] = e.value;
            foreach (var e in state.floats) floats[e.name] = e.value;
            foreach (var e in state.strings) strings[e.name] = e.value;
        }
    }
}
