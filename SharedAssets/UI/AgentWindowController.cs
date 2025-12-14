using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace GridWorld.UI
{
    public class AgentWindowController : IDisposable
    {
        private readonly VisualElement _root;
        private readonly VisualElement _content;
        private readonly VisualElement _header;
        private readonly Label _headerLabel;
        private readonly VisualElement _headerControlIcon;
        private readonly Label _headerControlLabel;

        private readonly WindowThemeSO _theme;
        private readonly Func<string> _agentNameProvider;
        private bool _isExpanded;

        private const string HiddenClassName = "hidden";

        public AgentWindowController(VisualElement rootElement, WindowThemeSO theme, Func<string> agentNameProvider)
        {
            _root = rootElement;
            _theme = theme;
            _agentNameProvider = agentNameProvider;

            _header = _root.Q<VisualElement>("ContainerHeader");
            _content = _root.Q<VisualElement>("ContainerContent");

            _headerLabel = _header?.Q<Label>("HeaderName");
            _headerControlIcon = _header?.Q<VisualElement>("ControlActionIcon");
            _headerControlLabel = _header?.Q<Label>("ControlActionName");

            if (_header != null && _content != null)
            {
                _header.RegisterCallback<ClickEvent>(OnHeaderClick);
            }

            // Determine initial state
            if (_content != null)
            {
                _isExpanded = !_content.ClassListContains(HiddenClassName);
            }

            UpdateVisuals();
        }

        /// <summary>
        /// Call this when the underlying agent data (the Bridge) has changed.
        /// </summary>
        public void Refresh()
        {
            UpdateVisuals();
        }

        private void OnHeaderClick(ClickEvent evt)
        {
            _isExpanded = !_isExpanded;
            _content?.ToggleInClassList(HiddenClassName);

            UpdateVisuals();
        }

        private void UpdateVisuals()
        {
            if (_theme == null) return;

            _theme.GetData(_isExpanded, out var icon, out var format, out var actionName);

            // Invoke the bridge to get the name of the CURRENTLY selected agent
            string currentAgentName = _agentNameProvider?.Invoke() ?? "No Agent Selected";

            if (_headerLabel != null)
            {
                _headerLabel.text = string.Format(format, currentAgentName);
            }

            if (_headerControlLabel != null)
            {
                _headerControlLabel.text = actionName;
            }

            if (_headerControlIcon != null && icon != null)
            {
                _headerControlIcon.style.backgroundImage = new StyleBackground(icon);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (_header != null && disposing)
            {
                _header.UnregisterCallback<ClickEvent>(OnHeaderClick);
            }
        }
    }
}