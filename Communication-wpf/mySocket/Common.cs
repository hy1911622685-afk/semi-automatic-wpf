using System;
using System.Text;
using System.Text.Json;

namespace Communication.Wpf.mySocket
{
    public enum MessageType
    {
        Text = 1,
        File = 2,
        Image = 3,
        System = 4,
        Login = 5,
        Logout = 6
    }

    public class MessageProtocol
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        public static byte[] PackMessage(string message)
        {
            if (message == null)
                message = string.Empty;

            if (!message.EndsWith("\r\n"))
                message += "\r\n";

            return Encoding.UTF8.GetBytes(message);
        }

        public static byte[] CreatePrivateMessage(string text)
        {
            return PackMessage($"[私聊消息]：{text}");
        }

        public static byte[] CreateSystemMessage(string content)
        {
            return PackMessage($"[系统广播]：{content}");
        }

        public static string ParseContent(byte[] contentBytes)
        {
            return contentBytes == null ? string.Empty : Encoding.UTF8.GetString(contentBytes);
        }
    }
}
