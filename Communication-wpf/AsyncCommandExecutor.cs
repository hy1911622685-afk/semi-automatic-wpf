using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Communication.Wpf
{
    public class AsyncCommandExecutor
    {
        private readonly Dictionary<string, Func<string, Task<string[]>>> _asyncCommands
            = new Dictionary<string, Func<string, Task<string[]>>>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<(string, string), string> _feedbackCommands
            = new Dictionary<(string, string), string>();

        public Func<string, Task<string[]>> MoveCenterFunc;
        public Func<string, Task<string[]>> MoveFrontFunc;
        public Func<string, Task<string[]>> MoveStopFunc;
        public Func<string, Task<string[]>> GetSelectDiesNumFunc;
        public Func<string, Task<string[]>> GetCurrentDiePosFunc;
        public Func<string, Task<string[]>> SetCurrentDieBinFunc;
        public Func<string, Task<string[]>> MoveFirstDieFunc;
        public Func<string, Task<string[]>> MoveNextDieFunc;
        public Func<string, Task<string[]>> MoveChuckHomeFunc;
        public Func<string, Task<string[]>> MoveDieFunc;
        public Func<string, Task<string[]>> ChuckContactFunc;
        public Func<string, Task<string[]>> ChuckSeparationFunc;
        public Func<string, Task<string[]>> UnloadWaferFunc;
        public Func<string, Task<string[]>> Get_seq_DiesFunc;

        public Func<string, string, Task> SendCmdAction;

        public void InitCmd()
        {
            _asyncCommands.Clear();
            _feedbackCommands.Clear();

            RegisterCommand("move_chuck_center\r\n", MoveCenterFunc);
            RegisterCommand("move_chuck_front\r\n", MoveFrontFunc);
            RegisterCommand("move_chuck_home\r\n", MoveChuckHomeFunc);
            RegisterCommand("move_stop\r\n", MoveStopFunc);
            RegisterCommand("map:step_first_die\r\n", MoveFirstDieFunc);
            RegisterCommand("map:step_next_die\r\n", MoveNextDieFunc);
            RegisterCommand("map:step_die", MoveDieFunc);
            RegisterCommand("move_chuck_contact\r\n", ChuckContactFunc);
            RegisterCommand("move_chuck_separation\r\n", ChuckSeparationFunc);
            RegisterCommand("map:get_num_dies\r\n", GetSelectDiesNumFunc);
            RegisterCommand("map:die:get_current_site\r\n", GetCurrentDiePosFunc);
            RegisterCommand("map:bins:set_bin", SetCurrentDieBinFunc);
            RegisterCommand("loader:unload_wafer\r\n", UnloadWaferFunc);
            RegisterCommand("map:get_seq_dies\r\n", Get_seq_DiesFunc);
        }

        public void RegisterCommand(string cmdKey, Func<string, Task<string[]>> handler)
        {
            if (string.IsNullOrWhiteSpace(cmdKey ) || handler == null)
                return;

            _asyncCommands[cmdKey ] = handler;
        }

        public void TripleAdd(string param1, string param2, string param3)
        {
            _feedbackCommands[(param1, param2)] = param3;
        }

        public async Task FeedbackCommand(string[] paramAry)
        {
            if (SendCmdAction == null || paramAry == null)
                return;

            if (paramAry.Length < 8)
                paramAry = paramAry.Concat(Enumerable.Repeat("", 8 - paramAry.Length)).ToArray();

            await SendCmdAction(string.Join(",", paramAry), null);
        }

        public async Task<string[]> ExecuteAsync(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return null;

            string param = string.Empty;
            string[] parts = command.Split(new[] { ' ' }, 2);
            string cmd = parts[0];

            if (parts.Length > 1)
                param = parts[1].Trim();

            return _asyncCommands.TryGetValue(cmd, out var handler) && handler != null
                ? await handler(param)
                : null;
        }
    }
}
