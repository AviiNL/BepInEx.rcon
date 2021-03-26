using BepInEx;

namespace rcon
{
    public abstract class AbstractCommand : ICommand
    {
        protected BaseUnityPlugin Plugin { get; private set; }

        void ICommand.setOwner(BaseUnityPlugin owner)
        {
            Plugin = owner;
        }

        public abstract string onCommand(string[] args);
    }
}
