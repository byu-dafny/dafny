﻿using MediatR;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Extensions.Logging;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Boogie;

// Justification: The handler must not await document loads. Errors are handled within the observer set up by ForwardDiagnostics.
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
#pragma warning disable VSTHRD110 // Observe result of async calls

// Justification: The task is launched within the same class
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks

namespace Microsoft.Dafny.LanguageServer.Handlers {
  /// <summary>
  /// LSP Synchronization handler for document based events, such as change, open, close and save.
  /// The documents are managed using an implementation of <see cref="IDocumentDatabase"/>.
  /// </summary>
  /// <remarks>
  /// The <see cref="CancellationToken"/> of all requests is not used here. The reason for this is because document changes are applied in the
  /// background to allow the request to complete immediately. This feature allows new document changes to be received an cancel any
  /// outstanding document changes.
  /// However, the cancellation token is marked as cancelled upon request completion. If it was used for the background processing, it would
  /// break the background processing if used.
  /// </remarks>
  public class DafnyTextDocumentHandler : TextDocumentSyncHandlerBase {
    private const string LanguageId = "dafny";

    private readonly ILogger logger;
    private readonly IDocumentDatabase documents;
    private readonly ITelemetryPublisher telemetryPublisher;
    private readonly IDiagnosticPublisher diagnosticPublisher;
    private readonly Dictionary<DocumentUri, RequestUpdatesOnUriObserver> observers = new();

    public DafnyTextDocumentHandler(
      ILogger<DafnyTextDocumentHandler> logger, IDocumentDatabase documents,
      ITelemetryPublisher telemetryPublisher, IDiagnosticPublisher diagnosticPublisher
    ) {
      this.logger = logger;
      this.documents = documents;
      this.telemetryPublisher = telemetryPublisher;
      this.diagnosticPublisher = diagnosticPublisher;
    }

    protected override TextDocumentSyncRegistrationOptions CreateRegistrationOptions(SynchronizationCapability capability, ClientCapabilities clientCapabilities) {
      return new TextDocumentSyncRegistrationOptions {
        DocumentSelector = DocumentSelector.ForLanguage(LanguageId),
        Change = TextDocumentSyncKind.Incremental
      };
    }

    public override TextDocumentAttributes GetTextDocumentAttributes(DocumentUri uri) {
      return new TextDocumentAttributes(uri, LanguageId);
    }

    public override Task<Unit> Handle(DidOpenTextDocumentParams notification, CancellationToken cancellationToken) {
      logger.LogTrace("received open notification {DocumentUri}", notification.TextDocument.Uri);
      ForwardDiagnostics(notification.TextDocument.Uri, documents.OpenDocument(DocumentTextBuffer.From(notification.TextDocument)));
      return Unit.Task;
    }

    public override Task<Unit> Handle(DidCloseTextDocumentParams notification, CancellationToken cancellationToken) {
      logger.LogTrace("received close notification {DocumentUri}", notification.TextDocument.Uri);
      CloseDocumentAndHideDiagnosticsAsync(notification.TextDocument);
      return Unit.Task;
    }

    public override Task<Unit> Handle(DidChangeTextDocumentParams notification, CancellationToken cancellationToken) {
      logger.LogTrace("received change notification {DocumentUri}", notification.TextDocument.Uri);
      ForwardDiagnostics(notification.TextDocument.Uri, documents.UpdateDocument(notification));
      return Unit.Task;
    }

    public override Task<Unit> Handle(DidSaveTextDocumentParams notification, CancellationToken cancellationToken) {
      logger.LogTrace("received save notification {DocumentUri}", notification.TextDocument.Uri);
      ForwardDiagnostics(notification.TextDocument.Uri, documents.SaveDocument(notification.TextDocument));
      return Unit.Task;
    }

    private void ForwardDiagnostics(DocumentUri uri, IObservable<DafnyDocument> requestUpdates) {
      var observer = observers.GetOrCreate(uri, () => new RequestUpdatesOnUriObserver(logger, telemetryPublisher, diagnosticPublisher));
      observer.OnNext(requestUpdates);
    }

    private class RequestUpdatesOnUriObserver : IObserver<IObservable<DafnyDocument>>, IDisposable {
      private readonly MergeOrdered<DafnyDocument> mergeOrdered;
      private readonly IDisposable subscription;

      public RequestUpdatesOnUriObserver(ILogger logger, ITelemetryPublisher telemetryPublisher,
        IDiagnosticPublisher diagnosticPublisher) {

        mergeOrdered = new MergeOrdered<DafnyDocument>();
        subscription = mergeOrdered.Subscribe(new DiagnosticsObserver(logger, telemetryPublisher, diagnosticPublisher));
      }

      public void Dispose() {
        subscription.Dispose();
      }

      public void OnCompleted() {
        mergeOrdered.OnCompleted();
      }

      public void OnError(Exception error) {
        mergeOrdered.OnError(error);
      }

      public void OnNext(IObservable<DafnyDocument> value) {
        mergeOrdered.OnNext(value);
      }
    }

    private class DiagnosticsObserver : IObserver<DafnyDocument> {
      private readonly ILogger logger;
      private readonly ITelemetryPublisher telemetryPublisher;
      private readonly IDiagnosticPublisher diagnosticPublisher;

      public DiagnosticsObserver(ILogger logger, ITelemetryPublisher telemetryPublisher, IDiagnosticPublisher diagnosticPublisher) {
        this.logger = logger;
        this.telemetryPublisher = telemetryPublisher;
        this.diagnosticPublisher = diagnosticPublisher;
      }

      public void OnCompleted() {
        telemetryPublisher.PublishUpdateComplete();
      }

      public void OnError(Exception e) {
        if (e is TaskCanceledException) {
          OnCompleted();
        } else {
          logger.LogError(e, "error while handling document event");
          telemetryPublisher.PublishUnhandledException(e);
        }
      }

      public void OnNext(DafnyDocument document) {
        diagnosticPublisher.PublishDiagnostics(document);
      }
    }

    private async Task CloseDocumentAndHideDiagnosticsAsync(TextDocumentIdentifier documentId) {
      try {
        await documents.CloseDocumentAsync(documentId);
      } catch (Exception e) {
        logger.LogError(e, "error while closing the document");
      }

      if (observers.TryGetValue(documentId.Uri, out var uriObserver)) {
        uriObserver.Dispose();
        observers.Remove(documentId.Uri);
      }
      diagnosticPublisher.HideDiagnostics(documentId);
    }
  }
}
