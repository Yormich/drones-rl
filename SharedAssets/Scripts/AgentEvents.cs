using System;

namespace Agents
{
    public static class AgentEvents
    {
        // One single event for the UI to listen to
        public static event Action<IAgent> OnAgentRegistered;
        public static event Action<IAgent> OnAgentUnregistered;

        public static event Action<IAgent> OnAgentEnvironmentReady;

        // Methods that Agents call to broadcast themselves
        public static void Register(IAgent agent)
        {
            OnAgentRegistered?.Invoke(agent);
        }

        public static void Unregister(IAgent agent)
        {
            OnAgentUnregistered?.Invoke(agent);
        }

        public static void EnvironmentReady(IAgent agent)
        {
            OnAgentEnvironmentReady?.Invoke(agent);
        }
    }
}