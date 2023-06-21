using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny; 

public class ContractIntegrity : IRewriter {
  internal ContractIntegrity(ErrorReporter reporter) : base(reporter) {
    Console.WriteLine("Got here.");
  }
  
  private bool ShouldGenerateChecker(MemberDecl decl) {
    return !decl.IsGhost &&
           decl is not Constructor
           && decl is Method method // TODO: should add functions, lemmas, etc later
           && (method.HasPrecondition || method.HasPostcondition);
  }
  
  internal override void PostResolveIntermediate(ModuleDefinition m) {
    // Keep a list of members to wrap so that we don't modify the collection we're iterating over.
    List<(TopLevelDeclWithMembers, MemberDecl)> membersToCheck = new();

    // Find module members to check.
    foreach (var topLevelDecl in m.TopLevelDecls.OfType<TopLevelDeclWithMembers>()) {
      foreach (var decl in topLevelDecl.Members) {
        if (ShouldGenerateChecker(decl)) {
          membersToCheck.Add((topLevelDecl, decl));
        }
      }
    }

    // Generate a check for each of the methods identified above.
    foreach (var (topLevelDecl, decl) in membersToCheck) {
      GeneratePreContradictionCheck(decl);
    }
    // callRedirector.NewRedirections = wrappedDeclarations;
  }

  // Only checks preconditions for contradictions. TODO: Add the other checks
  private void GeneratePreContradictionCheck(MemberDecl decl) {
    var name = decl.Name + "_contradiction_requires";
    
  }
}

// public class ContractIntegrityVisitor : TopDownVisitor<>

