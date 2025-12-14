using UnityEngine;

namespace Agents
{
    public interface IAgent
    {
        int AgentId { get; }
        string GetAgentName();
        Vector3 GetEnvironmentPosition();
    }
}