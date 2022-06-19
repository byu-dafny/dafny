﻿#nullable enable
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Boogie;
using Microsoft.Dafny.LanguageServer.Language;
using Microsoft.Dafny.LanguageServer.Language.Symbols;
using Microsoft.Dafny.LanguageServer.Workspace;
using Microsoft.Extensions.Logging;

namespace Microsoft.Dafny.LanguageServer.IntegrationTest.Diagnostics;

/// <summary>
/// This class extends the TextDocumentLoader but can record internals for testing purposes:
/// It currently records the priorities set before every verification round.
/// </summary>
public class ListeningTextDocumentLoader : TextDocumentLoader {
  public List<List<int>> LinearPriorities = new List<List<int>>();

  public ListeningTextDocumentLoader(
    ILoggerFactory loggerFactory, IDafnyParser parser,
    ISymbolResolver symbolResolver, IProgramVerifier verifier,
    ISymbolTableFactory symbolTableFactory,
    IGhostStateDiagnosticCollector ghostStateDiagnosticCollector,
    ICompilationStatusNotificationPublisher statusPublisher,
    INotificationPublisher notificationPublisher,
    VerifierOptions verifierOptions) : base(loggerFactory, parser, symbolResolver, verifier,
    symbolTableFactory, ghostStateDiagnosticCollector, statusPublisher, notificationPublisher,
    verifierOptions) {
  }


  protected override VerificationProgressReporter CreateVerificationProgressReporter(DafnyDocument document) {
    return new ListeningVerificationProgressReporter(
      loggerFactory.CreateLogger<ListeningVerificationProgressReporter>(),
      document, statusPublisher, NotificationPublisher, this);
  }

  public void RecordImplementationsPriority(List<int> priorityListPerImplementation) {
    LinearPriorities.Add(priorityListPerImplementation);
  }
}

public class ListeningVerificationProgressReporter : VerificationProgressReporter {
  public ListeningTextDocumentLoader TextDocumentLoader { get; }

  public ListeningVerificationProgressReporter(
    [NotNull] ILogger<VerificationProgressReporter> logger,
    [NotNull] DafnyDocument document,
    [NotNull] ICompilationStatusNotificationPublisher statusPublisher,
    [NotNull] INotificationPublisher notificationPublisher,
    ListeningTextDocumentLoader textDocumentLoader
    )
    : base(logger, document, statusPublisher, notificationPublisher) {
    TextDocumentLoader = textDocumentLoader;
  }

  public override void ReportImplementationsBeforeVerification(Implementation[] implementations) {
    base.ReportImplementationsBeforeVerification(implementations);
    TextDocumentLoader.RecordImplementationsPriority(implementations.Select(implementation => implementation.Priority)
      .ToList());
  }
}
