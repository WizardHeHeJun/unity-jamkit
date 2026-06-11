using System;
using UnityEngine;

namespace DialogueGraph
{
    public enum StageSlot
    {
        Left,
        Center,
        Right
    }

    public enum StageTransition
    {
        Cut,
        Fade
    }

    /// <summary>
    /// 演出指令基类。挂在对话节点（台词显示前执行）或演出节点上，
    /// 由表现层（VNStage）解释执行。新增指令：加子类 + StageCommandListView
    /// 注册编辑 UI + VNStage.Execute 加执行分支。
    /// </summary>
    [Serializable]
    public abstract class StageCommand
    {
    }

    [Serializable]
    public class ShowCharacterCommand : StageCommand
    {
        public CharacterAsset character;
        public string expression;
        public StageSlot slot = StageSlot.Center;
        public StageTransition transition = StageTransition.Fade;
        public float duration = 0.3f;
    }

    [Serializable]
    public class HideCharacterCommand : StageCommand
    {
        public CharacterAsset character;
        public StageTransition transition = StageTransition.Fade;
        public float duration = 0.3f;
    }

    [Serializable]
    public class SetBackgroundCommand : StageCommand
    {
        public Sprite background;
        public StageTransition transition = StageTransition.Fade;
        public float duration = 0.5f;
    }

    [Serializable]
    public class PlayBgmCommand : StageCommand
    {
        public AudioClip clip;
        public float fadeSeconds = 1f;
        public bool loop = true;
    }

    [Serializable]
    public class StopBgmCommand : StageCommand
    {
        public float fadeSeconds = 1f;
    }

    [Serializable]
    public class PlaySfxCommand : StageCommand
    {
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 1f;
    }

    [Serializable]
    public class WaitCommand : StageCommand
    {
        public float seconds = 0.5f;
    }
}
