using System;
using System.Collections.Generic;
using UnityEngine;
using Voxta.Unity.Protocol.Generated;

namespace Voxta.Unity
{
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

        public VoxtaActionDefinition() { }

        public VoxtaActionDefinition(string name, string description, params VoxtaActionArgument[] arguments)
        {
            this.name = name;
            this.description = description ?? string.Empty;
            this.arguments = arguments ?? Array.Empty<VoxtaActionArgument>();
        }

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
        [SerializeField] private VoxtaActionDefinition[] actions = Array.Empty<VoxtaActionDefinition>();
        private VoxtaChatSession session;

        public string ContextKey => contextKey;

        public void Bind(VoxtaChatSession value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (session == value) return;
            Unbind();
            session = value;
            session.Started += HandleSessionStarted;
            if (session.SessionId != Guid.Empty) Publish();
        }

        public void Unbind()
        {
            if (session != null) session.Started -= HandleSessionStarted;
            session = null;
        }

        /// <summary>Replaces the registered action set for the current chat, if one has started.</summary>
        public void SetActions(VoxtaActionDefinition[] value)
        {
            actions = value ?? Array.Empty<VoxtaActionDefinition>();
            if (session != null && session.SessionId != Guid.Empty) Publish();
        }

        public void Publish()
        {
            if (session == null || session.SessionId == Guid.Empty) return;
            if (string.IsNullOrWhiteSpace(contextKey)) throw new InvalidOperationException("Voxta actions need a context key.");
            var definitions = actions ?? Array.Empty<VoxtaActionDefinition>();
            var registered = new ScenarioActionDefinition[definitions.Length];
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < definitions.Length; index++)
            {
                if (definitions[index] == null) throw new InvalidOperationException("Voxta actions cannot be null.");
                var action = definitions[index].ToProtocolDefinition();
                if (!names.Add(action.Name)) throw new InvalidOperationException("Voxta action names must be unique.");
                registered[index] = action;
            }
            session.SendContextUpdate(new ClientUpdateContextMessage
            {
                SessionId = session.SessionId,
                ContextKey = contextKey,
                Actions = registered
            });
        }

        private void HandleSessionStarted(ServerChatStartedMessage _) => Publish();
        private void OnDisable() => Unbind();
    }
}
