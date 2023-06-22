using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dafny; 

public class ContractIntegrity : IRewriter {
  
  private readonly List<Method> checks = new();
  private ErrorReporter reporter;
  
  internal ContractIntegrity(ErrorReporter reporter) : base(reporter) {
    this.reporter = reporter;
  }
  
  // Determines whether a member declaration should have its contract checked
  private bool ShouldGenerateChecker(MemberDecl decl) {
    return !decl.IsGhost &&
           decl is not Constructor
           && decl is Method method // TODO: should add functions, lemmas, etc later: anything with a contract
           && (method.HasPrecondition || method.HasPostcondition);
  }
  
  // Inserts the contract checker methods
  internal override void PreResolve(ModuleDefinition m) {
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
      GeneratePreContradictionCheck((Method) decl, topLevelDecl);
    }
  }
  
  // Creates an assert statement that checks for contradiction
  private Statement CreateContradictionAssertStatement(AttributedExpression expr) {
    var tok = expr.E.tok;
    // var exprToCheck = expr.E;
    var exprToCheck = Expression.CreateNot(tok, expr.E);
    return new AssertStmt(expr.E.RangeToken, exprToCheck, null, null, new Attributes("subsumption 0", new List<Expression>(), null));
  }
  
  // Compiles all the asserts needed for a check and inserts them into the body
  private BlockStmt MakeContractCheckingBody(IEnumerable<AttributedExpression> requires) {
    var expectRequiresStmts = requires.Select(CreateContradictionAssertStatement);
    return new BlockStmt(RangeToken.NoToken, expectRequiresStmts.ToList());
  }
  
  // Checks preconditions for contradictions. TODO: Add the other checks
  private void GeneratePreContradictionCheck(Method decl, TopLevelDeclWithMembers parent) {
    var name = decl.Name + "_contradiction_requires";

    var ins_from_ins = decl.Ins;
    var ins_from_outs = decl.Outs;
    var ins = ins_from_ins.Concat(ins_from_outs).ToList();

    var body = MakeContractCheckingBody(decl.Req);

    var checkerMethod = new Method(RangeToken.NoToken, new Name(name), false, false, new List<TypeParameter>(),
      ins, new List<Formal>(), new List<AttributedExpression>(), new Specification<FrameExpression>(new List<FrameExpression>(), null), new List<AttributedExpression>(),
      new Specification<Expression>(new List<Expression>(), null), body, null, null) {
      EnclosingClass = parent
    };

    checks.Add(checkerMethod);
    parent.Members.Add(checkerMethod);
  }

  internal override void PostResolveIntermediate(ModuleDefinition moduleDefinition) {

  }
  
}

