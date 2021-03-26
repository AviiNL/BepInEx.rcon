using BepInEx;

namespace rcon
{
    internal interface ICommand
    {
        void setOwner(BaseUnityPlugin owner);
        string onCommand(string[] args);
    }
}
