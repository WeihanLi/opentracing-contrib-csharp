using System;
using Microsoft.Extensions.DiagnosticAdapter;
using Microsoft.Extensions.Logging;
using OpenTracing.Tag;

namespace OpenTracing.Contrib.NetCore.Interceptors.EntityFrameworkCore
{
    internal sealed class EntityFrameworkCoreInterceptor : DiagnosticInterceptor
    {
        private const string EventBeforeExecuteCommand = "Microsoft.EntityFrameworkCore.BeforeExecuteCommand";
        private const string EventAfterExecuteCommand = "Microsoft.EntityFrameworkCore.AfterExecuteCommand";
        private const string EventCommandExecutionError = "Microsoft.EntityFrameworkCore.CommandExecutionError";

        private const string Component = "EFCore";

        private const string TagCommandText = "ef.command";
        private const string TagMethod = "ef.method";
        private const string TagIsAsync = "ef.async";

        // https://github.com/aspnet/EntityFrameworkCore/blob/dev/src/EFCore/DbLoggerCategory.cs
        protected override string ListenerName => "Microsoft.EntityFrameworkCore";

        public EntityFrameworkCoreInterceptor(ILoggerFactory loggerFactory, ITracer tracer)
            : base(loggerFactory, tracer)
        {
        }

        /// <summary>
        /// Microsoft.EntityFrameworkCore.Relational/Internal/RelationalDiagnostics.cs
        /// </summary>
        [DiagnosticName(EventBeforeExecuteCommand)]
        public void OnBeforeExecuteCommand(IDbCommand command, string executeMethod, bool isAsync)
        {
            Execute(() =>
            {
                // TODO @cweiss !! OperationName ??
                string operationName = executeMethod;

                Tracer.BuildSpan(operationName)
                    .WithTag(Tags.SpanKind.Key, Tags.SpanKindClient)
                    .WithTag(Tags.Component.Key, Component)
                    .WithTag(TagCommandText, command.CommandText)
                    .WithTag(TagMethod, executeMethod)
                    .WithTag(TagIsAsync, isAsync)
                    .StartActive(finishSpanOnDispose: true);
            });
        }

        /// <summary>
        /// Microsoft.EntityFrameworkCore.Relational/Internal/RelationalDiagnostics.cs
        /// </summary>
        [DiagnosticName(EventAfterExecuteCommand)]
        public void OnAfterExecuteCommand(IDbCommand command, string executeMethod, bool isAsync)
        {
            DisposeActiveScope();
        }

        /// <summary>
        /// Microsoft.EntityFrameworkCore.Relational/Internal/RelationalDiagnostics.cs
        /// </summary>
        [DiagnosticName(EventCommandExecutionError)]
        public void OnCommandExecutionError(IDbCommand command, string executeMethod, bool isAsync, Exception exception)
        {
            Execute(() =>
            {
                var scope = Tracer.ScopeManager.Active;
                if (scope == null)
                {
                    Logger.LogError("Span not found");
                    return;
                }

                scope.Span.SetException(exception);
                scope.Dispose();
            });
        }
    }
}