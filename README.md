# RCON

This is an RCON library for BepInEx compatible games that do not ship with a native RCON  implementation, like Valheim.

## Usage

Add `rcon.dll` to your `BepInEx/plugins` folder. Run the server at least once to generate the required config files.

### Configuration
The configuration file will be generated as `BepInEx/config/nl.avii.plugins.rcon.cfg`

Open this file with your favorite editor and change `enabled` to `true`.
 Change the port number to something close to the game port, for Valheim I've used `GamePort+2`, and at least on Nitrado game servers, I am able to connect to it. (not sure about other providers)

> NOTE: If you host your own dedicated server, make sure to port forward the selected port if you wish to connect to it remotely.

The `password` field is self-explanatory.

After changing the values, restart the server.

## Commands

This library does not ship with commands but is intended to be used as a dependency for other plugins.

Add a reference to `rcon.dll` to your own project

### Adding a command

There are 2 ways to register commands from your own plugins.

Inline command example
```csharp
[BepInPlugin("com.bepinex.plugins.example", "Example", "1.0")]
[BepInDependency("nl.avii.plugins.rcon", BepInDependency.DependencyFlags.HardDependency)]
[BepInProcess("valheim_server.exe")]
public class Plugin : BaseUnityPlugin
{
    rcon.rcon RCON;
 
    void OnEnable()
    {
        RCON = GetComponent<rcon.rcon>();
        if (RCON == null)
        {
            Logger.LogError("rcon plugin not loaded");
            return;
        }
 
        RCON.RegisterCommand(this, "test", (args) =>
        {
            Logger.LogInfo("Command 'test' execution");
 
            return "string to return to rcon client";
        });
    }
 
    void OnDisable()
    {
        if (RCON == null)
        {
            Logger.LogError("rcon plugin not loaded");
            return;
        }
 
        RCON.UnRegisterCommand(this, "test");
    }
}
```
Or using a separate class handler, register the command in `OnEnable()` of your plugin:
```csharp
RCON.RegisterCommand<TestCommand>(this, "test");
```
And Unregister in `OnDisable()`
```csharp
RCON.UnRegisterCommand(this, "test");
```

Add create a `TestCommand.cs` class
```csharp
using rcon;
class TestCommand : AbstractCommand
{
    public override string onCommand(string[] args)
    {
        // This will be executed on `test`
        // There is a protected property called "Plugin"
        // which holds a reference to your plugin class
        return "string to return to rcon client";
    }
}
```

### Unknown Command Handler

If an issued command can not be found, the library fires an the  `rcon.OnUnknownCommand` event.

This is an example to forward unknown commands to a connected client based on their `steamid`

```csharp
void OnEnable()
{
    ...
    
    RCON.OnUnknownCommand += RCON_OnUnknownCommand;
}

private string RCON_OnUnknownCommand(string command, string[] args)
{
    var arguments = args.ToList();
    string steamid = arguments.First();
    if (steamid == null)
    {
        return "Unknown command";
    }
 
    List<ZNetPeer> peers = (List<ZNetPeer>)AccessTools.Field(typeof(ZNet), "m_peers").GetValue(ZNet.instance);
    var peer = peers.Where(x => ((ZSteamSocket)x.m_socket).GetPeerID().ToString().Trim().ToLower() == steamid.Trim().ToLower()).FirstOrDefault();
    if (peer == null)
        return $"Player with steamid {steamid} not connected";
 
    arguments.RemoveAt(0);
 
    ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, "ExamplePlugin", command, arguments);
 
    return $"Command {command} forwarded to {peer.m_uid}";
}
```