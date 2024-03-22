using BepInEx;
using BepInEx.Configuration;
using rcon.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace rcon;

[BepInPlugin("nl.avii.plugins.rcon", "rcon", "1.0.3")]
public class rcon : BaseUnityPlugin
{
    public delegate string UnknownCommand(string command, string[] args);
    public event UnknownCommand? OnUnknownCommand;

    public delegate string ParamsAction(params string[] args);
    private readonly AsynchronousSocketListener _socketListener;

    private readonly ConfigEntry<bool> _enabled;
    private readonly ConfigEntry<int> _port;
    private readonly ConfigEntry<string> _password;

    private readonly Dictionary<string, Type> _commands = new();
    private readonly Dictionary<string, ParamsAction> _customCommands = new();

    private readonly Dictionary<string, BaseUnityPlugin> _owners = new();

    private rcon()
    {
        _enabled = Config.Bind("rcon", "enabled", false, "Enable RCON Communication");
        _port = Config.Bind("rcon", "port", 2458, "Port to use for RCON Communication");
        _password = Config.Bind("rcon", "password", "ChangeMe", "Password to use for RCON Communication");
        _socketListener = new AsynchronousSocketListener(IPAddress.Any, _port.Value);
        _socketListener.OnMessage += SocketListener_OnMessage;
    }

    private void Awake()
    {
        InvokeRepeating(nameof(Cleanup), 1f, 1f);
    }

    private void OnEnable()
    {
        if (!_enabled.Value) return;
        _socketListener.StartListening();
        Logger.LogInfo("RCON Listening on port: " + _port.Value);
    }

    private void Cleanup()
    {
        _socketListener.Cleanup();
    }
    
    private void SocketListener_OnMessage(Socket socket, int requestId, PacketType type, string payload)
    {
        switch (type)
        {
            case PacketType.Login:

                var responsePayload = "Login Success";
                if (payload.Trim() != _password.Value.Trim())
                {
                    responsePayload = "Login Failed";
                    requestId = -1;
                }

                byte[] packet = PacketBuilder.CreatePacket(requestId, PacketType.LoginResponse, responsePayload);

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

                if (!_commands.ContainsKey(command) && !_customCommands.ContainsKey(command))
                {
                    var ret = OnUnknownCommand?.Invoke(command, data.ToArray()) ?? string.Empty;
                    var cmd = PacketType.Command;
                    if (ret.ToLower().Contains("unknown"))
                        cmd = PacketType.Error;

                    socket.Send(PacketBuilder.CreatePacket(requestId, cmd, ret));
                    return;
                }

                if (_commands.TryGetValue(command, out var t))
                {
                    var instance = (ICommand) Activator.CreateInstance(t);
                    instance.setOwner(_owners[command]);
                    var response = instance.onCommand(data.ToArray());
                    socket.Send(PacketBuilder.CreatePacket(requestId, type, response));
                }
                else if (_customCommands.TryGetValue(command, out var customCommand))
                {
                    var response = customCommand(data.ToArray());
                    socket.Send(PacketBuilder.CreatePacket(requestId, type, response));
                }
                break;
            default:
                Logger.LogError($"Unknown packet type: {type}");
                break;
        }
    }
    
    private void OnDisable()
    {
        if (!_enabled.Value) return;
        _socketListener.Close();
    }

    public void RegisterCommand<T>(BaseUnityPlugin owner, string command) where T : AbstractCommand, new()
    {
        command = command.ToLower();
        if (_owners.ContainsKey(command))
        {
            Logger.LogError($"{command} already registered");
            return;
        }
        _owners[command] = owner;
        _commands[command] = typeof(T);
        Logger.LogInfo($"Registering Command: {command}");
    }

    public void RegisterCommand(BaseUnityPlugin owner, string command, ParamsAction action)
    {
        command = command.ToLower();
        if (_owners.ContainsKey(command))
        {
            Logger.LogError($"{command} already registered");
            return;
        }
        _owners[command] = owner;
        _customCommands[command] = action;
        Logger.LogInfo($"Registering Command: {command}");
    }

    public void UnRegisterCommand(BaseUnityPlugin owner, string command)
    {
        if (!_owners.ContainsKey(command))
        {
            return;
        }

        if (_owners[command] != owner)
        {
            return;
        }

        _owners.Remove(command);

        if (_commands.ContainsKey(command))
            _commands.Remove(command);

        if (_customCommands.ContainsKey(command))
            _customCommands.Remove(command);
    }
}