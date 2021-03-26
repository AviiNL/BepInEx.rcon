using BepInEx;
using BepInEx.Configuration;
using rcon.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace rcon
{
    [BepInPlugin("nl.avii.plugins.rcon", "rcon", "1.0")]
    public class rcon : BaseUnityPlugin
    {
        public delegate string UnknownCommand(string command, string[] args);
        public event UnknownCommand OnUnknownCommand;

        public delegate string ParamsAction(params object[] args);
        private AsynchronousSocketListener socketListener;

        private ConfigEntry<bool> Enabled;
        private ConfigEntry<int> Port;
        private ConfigEntry<string> Password;

        private Dictionary<string, Type> commands = new Dictionary<string, Type>();
        private Dictionary<string, ParamsAction> customCommands = new Dictionary<string, ParamsAction>();

        private Dictionary<string, BaseUnityPlugin> owners = new Dictionary<string, BaseUnityPlugin>();

        private rcon()
        {
            Enabled = Config.Bind("rcon", "enabled", false, "Enable RCON Communication");
            Port = Config.Bind("rcon", "port", 2458, "Port to use for RCON Communication");
            Password = Config.Bind("rcon", "password", "ChangeMe", "Password to use for RCON Communication");
        }

        private void OnEnable()
        {
            if (!Enabled.Value) return;
            socketListener = new AsynchronousSocketListener();
            socketListener.OnMessage += SocketListener_OnMessage;

            Logger.LogInfo("RCON Listening on port: " + Port.Value);
            socketListener.StartListening(Port.Value);
        }

        private void SocketListener_OnMessage(Socket socket, int requestId, PacketType type, string payload)
        {
            switch (type)
            {
                case PacketType.Login:

                    string response_payload = "Login Success";
                    if (payload.Trim() != Password.Value.Trim())
                    {
                        response_payload = "Login Failed";
                        requestId = -1;
                    }

                    byte[] packet = PacketBuilder.CreatePacket(requestId, type, response_payload);

                    socket.Send(packet);
                    break;
                case PacketType.Command:
                    // strip slash if present
                    if (payload[0] == '/')
                    {
                        payload = payload.Substring(1);
                    }

                    var data = Regex.Matches(payload, @"(?<=[ ][\""]|^[\""])[^\""]+(?=[\""][ ]|[\""]$)|(?<=[ ]|^)[^\"" ]+(?=[ ]|$)")
                        .Cast<Match>()
                        .Select(m => m.Value)
                        .ToList();

                    string command = data[0].ToLower();
                    data.RemoveAt(0);

                    if (!commands.ContainsKey(command) && !customCommands.ContainsKey(command))
                    {
                        var ret = OnUnknownCommand?.Invoke(command, data.ToArray());
                        PacketType t = PacketType.Command;
                        if (ret.ToLower().Contains("unknown"))
                            t = PacketType.Error;

                        socket.Send(PacketBuilder.CreatePacket(requestId, t, ret));
                        return;
                    }

                    if (commands.ContainsKey(command))
                    {
                        var t = commands[command];
                        var instance = (ICommand)Activator.CreateInstance(t);
                        instance.setOwner(owners[command]);
                        var response = instance.onCommand(data.ToArray());
                        socket.Send(PacketBuilder.CreatePacket(requestId, type, response));
                    }
                    else if (customCommands.ContainsKey(command))
                    {
                        var response = customCommands[command](data.ToArray());
                        socket.Send(PacketBuilder.CreatePacket(requestId, type, response));
                    }
                    break;
                default:
                    Logger.LogError($"Unknown packet type: {type}");
                    break;
            }
        }

        private void Update()
        {
            if (!Enabled.Value) return;
            if (socketListener == null) return;

            socketListener.Update();
        }

        private void OnDisable()
        {
            if (!Enabled.Value) return;
            socketListener.Close();
        }

        public void RegisterCommand<T>(BaseUnityPlugin owner, string command) where T : AbstractCommand, new()
        {
            command = command.ToLower();
            if (owners.ContainsKey(command))
            {
                Logger.LogError($"{command} already registered");
                return;
            }
            owners[command] = owner;
            commands[command] = typeof(T);
            Logger.LogInfo($"Registering Command: {command}");
        }

        public void RegisterCommand(BaseUnityPlugin owner, string command, ParamsAction action)
        {
            command = command.ToLower();
            if (owners.ContainsKey(command))
            {
                Logger.LogError($"{command} already registered");
                return;
            }
            owners[command] = owner;
            customCommands[command] = action;
            Logger.LogInfo($"Registering Command: {command}");
        }

        public void UnRegisterCommand(BaseUnityPlugin owner, string command)
        {
            if (!owners.ContainsKey(command))
            {
                return;
            }

            if (owners[command] != owner)
            {
                return;
            }

            owners.Remove(command);

            if (commands.ContainsKey(command))
                commands.Remove(command);

            if (customCommands.ContainsKey(command))
                customCommands.Remove(command);
        }
    }
}
