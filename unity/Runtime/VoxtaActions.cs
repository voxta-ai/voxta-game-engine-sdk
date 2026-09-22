using System;
using System.Collections.Generic;
using UnityEngine;
using Voxta.Unity.Protocol.Generated;

namespace Voxta.Unity
{
    [Serializable] public sealed class VoxtaActionUnityEvent : UnityEngine.Events.UnityEvent<ServerActionMessage> { }

    [Serializable]
    public sealed class VoxtaContextDefinition
    {
        [SerializeField] private string name = "";
        [TextArea] [SerializeField] private string text = "";
        [SerializeField] private bool disabled;
        [SerializeField] private string flagsFilter = "";
        [SerializeField] private string roleFilter = "";
        [SerializeField] private PromptCategories applyTo = PromptCategories.All;
        [SerializeField] private PromptPositions position = PromptPositions.Context;

        public VoxtaContextDefinition() { }

        public VoxtaContextDefinition(string text, string name = null)
        {
            this.text = text ?? string.Empty;
            this.name = name ?? string.Empty;
        }

        internal ContextDefinition ToProtocolDefinition()
        {
            return new ContextDefinition
            {
                Name = string.IsNullOrWhiteSpace(name) ? null : name,
                Text = text ?? string.Empty,
                Disabled = disabled,
                FlagsFilter = string.IsNullOrWhiteSpace(flagsFilter) ? null : flagsFilter,
                RoleFilter = string.IsNullOrWhiteSpace(roleFilter) ? null : roleFilter,
                ApplyTo = applyTo,
                Position = position
            };
        }
    }

    [Serializable]
    public sealed class VoxtaActionArgument
    {
        [SerializeField] private string name = "";
        [SerializeField] private FunctionArgumentType type = FunctionArgumentType.String;
        [SerializeField] private string description = "";
        [SerializeField] private bool required;
        [SerializeField] private FunctionArgumentType itemsType = FunctionArgumentType.String;
        [SerializeField] private string[] choices = Array.Empty<string>();

        public VoxtaActionArgument() { }

        public VoxtaActionArgument(string name, FunctionArgumentType type = FunctionArgumentType.String, string description = null, bool required = false)
        {
            this.name = name;
            this.type = type;
            this.description = description ?? string.Empty;
            this.required = required;
        }

        internal FunctionArgumentDefinition ToProtocolDefinition()
        {
            return new FunctionArgumentDefinition
            {
                Name = name,
                Type = type,
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                Required = required,
                ItemsType = type == FunctionArgumentType.Array ? itemsType : (FunctionArgumentType?)null,
                Choices = choices == null || choices.Length == 0 ? null : choices
            };
        }
    }

    [Serializable]
    public sealed class VoxtaActionDefinition
    {
        [SerializeField] private string name = "";
        [SerializeField] private string description = "";
        [SerializeField] private VoxtaActionArgument[] arguments = Array.Empty<VoxtaActionArgument>();
        [SerializeField] private VoxtaActionUnityEvent onInvoked = new VoxtaActionUnityEvent();

        public VoxtaActionDefinition() { }

        public VoxtaActionDefinition(string name, string description, params VoxtaActionArgument[] arguments)
        {
            this.name = name;
            this.description = description ?? string.Empty;
            this.arguments = arguments ?? Array.Empty<VoxtaActionArgument>();
        }

        internal string Name => name;
        public VoxtaActionUnityEvent OnInvoked => onInvoked;

        internal ScenarioActionDefinition ToProtocolDefinition()
        {
            if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Every Voxta action needs a name.");
            return new ScenarioActionDefinition
            {
                Name = name,
                Description = description ?? string.Empty,
                Arguments = ConvertArguments(arguments)
            };
        }

        internal void Invoke(ServerActionMessage action) => onInvoked.Invoke(action);

        private static FunctionArgumentDefinition[] ConvertArguments(VoxtaActionArgument[] definitions)
        {
            if (definitions == null || definitions.Length == 0) return Array.Empty<FunctionArgumentDefinition>();
            var result = new FunctionArgumentDefinition[definitions.Length];
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < definitions.Length; index++)
            {
                if (definitions[index] == null) throw new InvalidOperationException("Voxta action arguments cannot be null.");
                var argument = definitions[index].ToProtocolDefinition();
                if (string.IsNullOrWhiteSpace(argument.Name)) throw new InvalidOperationException("Every Voxta action argument needs a name.");
                if (!names.Add(argument.Name)) throw new InvalidOperationException("Voxta action argument names must be unique.");
                result[index] = argument;
            }
            return result;
        }
    }

    /// <summary>Publishes this GameObject's declared actions after its companion starts a chat.</summary>
    public sealed class VoxtaActions : MonoBehaviour
    {
        [SerializeField] private string contextKey = "Unity";
        [SerializeField] private VoxtaContextDefinition[] contexts = Array.Empty<VoxtaContextDefinition>();
        [SerializeField] private VoxtaActionDefinition[] actions = Array.Empty<VoxtaActionDefinition>();
        [SerializeField] private VoxtaActionUnityEvent onAction = new VoxtaActionUnityEvent();
        private VoxtaChatSession session;
        private readonly Dictionary<string, Action<ServerActionMessage>> handlers = new Dictionary<string, Action<ServerActionMessage>>(StringComparer.Ordinal);

        public string ContextKey => contextKey;
        public VoxtaActionUnityEvent OnAction => onAction;
        public event Action<ServerActionMessage> ActionReceived;

        public void Bind(VoxtaChatSession value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (session == value) return;
            Unbind();
            session = value;
            session.Started += HandleSessionStarted;
            session.ActionReceived += HandleActionReceived;
            if (session.SessionId != Guid.Empty) Publish();
        }

        public void Unbind()
        {
            if (session != null)
            {
                session.Started -= HandleSessionStarted;
                session.ActionReceived -= HandleActionReceived;
            }
            session = null;
        }

        /// <summary>Registers a handler for an action name published by this component.</summary>
        public void RegisterHandler(string actionName, Action<ServerActionMessage> handler)
        {
            if (string.IsNullOrWhiteSpace(actionName)) throw new ArgumentException("An action name is required.", nameof(actionName));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (handlers.TryGetValue(actionName, out var existing)) handlers[actionName] = existing + handler;
            else handlers.Add(actionName, handler);
        }

        /// <summary>Removes a handler previously registered for an action name.</summary>
        public void UnregisterHandler(string actionName, Action<ServerActionMessage> handler)
        {
            if (string.IsNullOrWhiteSpace(actionName)) throw new ArgumentException("An action name is required.", nameof(actionName));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!handlers.TryGetValue(actionName, out var existing)) return;
            existing -= handler;
            if (existing == null) handlers.Remove(actionName);
            else handlers[actionName] = existing;
        }

        /// <summary>Replaces the registered action set for the current chat, if one has started.</summary>
        public void SetActions(VoxtaActionDefinition[] value)
        {
            actions = value ?? Array.Empty<VoxtaActionDefinition>();
            if (session != null && session.SessionId != Guid.Empty) Publish();
        }

        /// <summary>Replaces the scenario contexts registered for this component's context key.</summary>
        public void SetContexts(VoxtaContextDefinition[] value)
        {
            contexts = value ?? Array.Empty<VoxtaContextDefinition>();
            if (session != null && session.SessionId != Guid.Empty) Publish();
        }

        public void Publish()
        {
            if (session == null || session.SessionId == Guid.Empty) return;
            if (string.IsNullOrWhiteSpace(contextKey)) throw new InvalidOperationException("Voxta actions need a context key.");
            var definitions = actions ?? Array.Empty<VoxtaActionDefinition>();
            var registered = new ScenarioActionDefinition[definitions.Length];
            var contextDefinitions = contexts ?? Array.Empty<VoxtaContextDefinition>();
            var registeredContexts = new ContextDefinition[contextDefinitions.Length];
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < definitions.Length; index++)
            {
                if (definitions[index] == null) throw new InvalidOperationException("Voxta actions cannot be null.");
                var action = definitions[index].ToProtocolDefinition();
                if (!names.Add(action.Name)) throw new InvalidOperationException("Voxta action names must be unique.");
                registered[index] = action;
            }
            for (var index = 0; index < contextDefinitions.Length; index++)
            {
                if (contextDefinitions[index] == null) throw new InvalidOperationException("Voxta context definitions cannot be null.");
                registeredContexts[index] = contextDefinitions[index].ToProtocolDefinition();
            }
            session.SendContextUpdate(new ClientUpdateContextMessage
            {
                SessionId = session.SessionId,
                ContextKey = contextKey,
                Contexts = registeredContexts,
                Actions = registered
            });
        }

        private void HandleSessionStarted(ServerChatStartedMessage _) => Publish();

        private void HandleActionReceived(ServerActionMessage action)
        {
            if (!string.Equals(action.ContextKey, contextKey, StringComparison.Ordinal)) return;
            if (handlers.TryGetValue(action.Value, out var handler)) handler.Invoke(action);
            ActionReceived?.Invoke(action);
            onAction.Invoke(action);
            var definitions = actions ?? Array.Empty<VoxtaActionDefinition>();
            for (var index = 0; index < definitions.Length; index++)
                if (definitions[index] != null && string.Equals(definitions[index].Name, action.Value, StringComparison.Ordinal))
                    definitions[index].Invoke(action);
        }
        private void OnDisable() => Unbind();
    }
}
