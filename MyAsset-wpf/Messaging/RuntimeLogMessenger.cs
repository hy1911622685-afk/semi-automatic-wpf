using CommunityToolkit.Mvvm.Messaging;

namespace MyAsset.Wpf.Messaging
{
    public static class RuntimeLogMessenger
    {
        public static void Broadcast(string project, string module, string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            WeakReferenceMessenger.Default.Send(new RuntimeLogMessage(
                project ?? string.Empty,
                module ?? string.Empty,
                message));
        }
    }

    public sealed class RuntimeLogMessage
    {
        public RuntimeLogMessage(string project, string module, string message)
        {
            Project = project;
            Module = module;
            Message = message;
        }

        public string Project { get; }

        public string Module { get; }

        public string Message { get; }
    }
}
