using System.Collections.Generic;

namespace DialogueGraph.Editor
{
    /// <summary>
    /// 演出指令剪贴板：「舞台布景」窗口生成布景指令存入，
    /// 节点上的演出指令列表（StageCommandListView）「粘贴布景」取出。
    /// 每次取出都是深拷贝，可多次粘贴互不共享实例。
    /// </summary>
    public static class StageCommandClipboard
    {
        static readonly List<StageCommand> commands = new List<StageCommand>();

        public static int Count => commands.Count;

        public static void Set(IEnumerable<StageCommand> source)
        {
            commands.Clear();
            foreach (var command in source)
                commands.Add(Clone(command));
        }

        public static List<StageCommand> CloneAll()
        {
            return commands.ConvertAll(Clone);
        }

        static StageCommand Clone(StageCommand command)
        {
            switch (command)
            {
                case ShowCharacterCommand c:
                    return new ShowCharacterCommand
                    {
                        character = c.character,
                        expression = c.expression,
                        slot = c.slot,
                        transition = c.transition,
                        duration = c.duration
                    };
                case HideCharacterCommand c:
                    return new HideCharacterCommand
                    {
                        character = c.character,
                        transition = c.transition,
                        duration = c.duration
                    };
                case SetBackgroundCommand c:
                    return new SetBackgroundCommand
                    {
                        background = c.background,
                        transition = c.transition,
                        duration = c.duration
                    };
                case PlayBgmCommand c:
                    return new PlayBgmCommand { clip = c.clip, fadeSeconds = c.fadeSeconds, loop = c.loop };
                case StopBgmCommand c:
                    return new StopBgmCommand { fadeSeconds = c.fadeSeconds };
                case PlaySfxCommand c:
                    return new PlaySfxCommand { clip = c.clip, volume = c.volume };
                case WaitCommand c:
                    return new WaitCommand { seconds = c.seconds };
                default:
                    return command;
            }
        }
    }
}
