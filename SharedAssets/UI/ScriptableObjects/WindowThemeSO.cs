using UnityEngine;

namespace GridWorld.UI
{
    [CreateAssetMenu(fileName = "NewWindowTheme", menuName = "UI/Window Theme")]
    public class WindowThemeSO : ScriptableObject
    {
        [Header("Collapsed State")]
        public Texture2D CollapsedIcon;
        [Tooltip("Use {0} as a placeholder for the Agent's data string")]
        public string CollapsedTextFormat = "Show {0} Details";
        public string CollapsedActionName = "Show";

        [Header("Expanded State")]
        public Texture2D ExpandedIcon;
        [Tooltip("Use {0} as a placeholder for the Agent's string")]
        public string ExpandedTextFormat = "{0} Data";
        public string ExpandedActionName = "Hide";

        // Helper to get data dynamically
        public void GetData(bool isExpanded, out Texture2D icon, out string format, out string action)
        {
            if (isExpanded)
            {
                icon = ExpandedIcon;
                format = ExpandedTextFormat;
                action = ExpandedActionName;
            }
            else
            {
                icon = CollapsedIcon;
                format = CollapsedTextFormat;
                action = CollapsedActionName;
            }
        }
    }
}