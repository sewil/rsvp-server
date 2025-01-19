using System;

namespace WvsBeta.SharedDataProvider.Providers
{
    public class TemplateException : Exception
    {
        private readonly Type _providerType;
        private readonly string _message;

        public TemplateException(Type ProviderType, string Message = "")
        {
            _providerType = ProviderType;
            _message = Message;
        }

        public override string Message => $"Error when loading template with type {_providerType}: {_message}";
    }
}