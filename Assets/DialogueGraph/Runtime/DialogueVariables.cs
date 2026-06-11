using System;
using System.Collections.Generic;

namespace DialogueGraph
{
    /// <summary>变量表快照，可直接 JsonUtility 序列化（随存档保存）。</summary>
    [Serializable]
    public class VariableStoreState
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
    /// 条件节点求值时的变量来源。接到游戏自己的存档 / 任务系统上，
    /// 或直接使用现成的 <see cref="DialogueVariableStore"/>。
    /// </summary>
    public interface IDialogueVariableProvider
    {
        bool TryGetBool(string name, out bool value);
        bool TryGetInt(string name, out int value);
        bool TryGetFloat(string name, out float value);
        bool TryGetString(string name, out string value);
    }

    /// <summary>简单的内存变量表实现。</summary>
    public class DialogueVariableStore : IDialogueVariableProvider
    {
        readonly Dictionary<string, bool> bools = new Dictionary<string, bool>();
        readonly Dictionary<string, int> ints = new Dictionary<string, int>();
        readonly Dictionary<string, float> floats = new Dictionary<string, float>();
        readonly Dictionary<string, string> strings = new Dictionary<string, string>();

        public void SetBool(string name, bool value) => bools[name] = value;
        public void SetInt(string name, int value) => ints[name] = value;
        public void SetFloat(string name, float value) => floats[name] = value;
        public void SetString(string name, string value) => strings[name] = value;

        /// <summary>移除任意类型下的同名变量。</summary>
        public void Remove(string name)
        {
            bools.Remove(name);
            ints.Remove(name);
            floats.Remove(name);
            strings.Remove(name);
        }

        public bool TryGetBool(string name, out bool value) => bools.TryGetValue(name, out value);
        public bool TryGetInt(string name, out int value) => ints.TryGetValue(name, out value);
        public bool TryGetFloat(string name, out float value) => floats.TryGetValue(name, out value);
        public bool TryGetString(string name, out string value) => strings.TryGetValue(name, out value);

        public void Clear()
        {
            bools.Clear();
            ints.Clear();
            floats.Clear();
            strings.Clear();
        }

        public VariableStoreState CaptureState()
        {
            var state = new VariableStoreState();
            foreach (var kv in bools)
                state.bools.Add(new VariableStoreState.BoolEntry { name = kv.Key, value = kv.Value });
            foreach (var kv in ints)
                state.ints.Add(new VariableStoreState.IntEntry { name = kv.Key, value = kv.Value });
            foreach (var kv in floats)
                state.floats.Add(new VariableStoreState.FloatEntry { name = kv.Key, value = kv.Value });
            foreach (var kv in strings)
                state.strings.Add(new VariableStoreState.StringEntry { name = kv.Key, value = kv.Value });
            return state;
        }

        public void ApplyState(VariableStoreState state)
        {
            Clear();
            if (state == null) return;
            foreach (var e in state.bools) bools[e.name] = e.value;
            foreach (var e in state.ints) ints[e.name] = e.value;
            foreach (var e in state.floats) floats[e.name] = e.value;
            foreach (var e in state.strings) strings[e.name] = e.value;
        }
    }
}
