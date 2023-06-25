using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;

namespace Microsoft.Dafny; 

public class ContractIntegrity : IRewriter {
  
  private readonly List<Method> checks = new();
  private ErrorReporter reporter;
  private VacuityVisitor visitor = new VacuityVisitor();
  
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
      GenerateContradictionCheck((Method) decl, topLevelDecl, true); // precondition contradiction check
      GenerateContradictionCheck((Method) decl, topLevelDecl, false); // postcondition contradiction check
      GenerateVacuityCheck((Method) decl, topLevelDecl, true); // precondition vacuity check
    }
  }
  
  // Creates an assert statement that checks for contradiction
  private Statement CreateContradictionAssertStatement(Expression expr) {
    var tok = expr.tok;
    var exprToCheck = Expression.CreateNot(tok, expr);
    return new AssertStmt(expr.RangeToken, exprToCheck, null, null, new Attributes("subsumption 0", new List<Expression>(), null));
  }
  
  // Compiles all the asserts needed for a check and inserts them into the body
  private BlockStmt MakeContradictionCheckingBody(IEnumerable<AttributedExpression> constraints) {
    var expectStmts = new List<Statement>();
    foreach (var constraint in constraints) {
      expectStmts.Add(CreateContradictionAssertStatement(constraint.E));
    }
    return new BlockStmt(RangeToken.NoToken, expectStmts);
  }
  
  // Checks preconditions for contradictions. TODO: Add the other checks
  private void GenerateContradictionCheck(Method decl, TopLevelDeclWithMembers parent, bool precondition) {
    string nameSuffix;
    BlockStmt body;
    if (precondition) {
      nameSuffix = "_contradiction_requires";
      body = MakeContradictionCheckingBody(decl.Req);
    } else {
      nameSuffix = "_contradiction_ensures";
      body = MakeContradictionCheckingBody(decl.Ens);
    }
    
    GenerateMethodCheck(decl, parent, nameSuffix, body);
  }

  // Compiles all the asserts needed for a check and inserts them into the body
  private BlockStmt MakeVacuityCheckingBody(IEnumerable<AttributedExpression> constraints) {

    List<Expression> toTest = new List<Expression>();
    foreach (var constraint in constraints)
    {
      visitor.Visit(constraint.E, constraint);
    }
    
    toTest.AddRange(visitor.ClausesToCheck);
    
    var expectStmts = toTest.Select(CreateContradictionAssertStatement);
    return new BlockStmt(RangeToken.NoToken, expectStmts.ToList());
  }
  
  private void GenerateVacuityCheck(Method decl, TopLevelDeclWithMembers parent, bool precondition) {
    string nameSuffix;
    BlockStmt body;
    
    if (precondition) {
      nameSuffix = "_vacuity_requires";
      body = MakeVacuityCheckingBody(decl.Req);
    } else {
      nameSuffix = "_vacuity_ensures";
      body = MakeVacuityCheckingBody(decl.Ens);
    }
    
    GenerateMethodCheck(decl, parent, nameSuffix, body);
  }

  private void GenerateMethodCheck(Method decl, TopLevelDeclWithMembers parent, string nameSuffix, BlockStmt body) {
    string name = decl.Name + nameSuffix;
    
    // inputs to each check for the given method
    var ins_from_ins = decl.Ins;
    var ins_from_outs = decl.Outs;
    var ins = ins_from_ins.Concat(ins_from_outs).ToList();

    var checkerMethod = new Method(RangeToken.NoToken, new Name(name), false, false, new List<TypeParameter>(),
      ins, new List<Formal>(), new List<AttributedExpression>(), new Specification<FrameExpression>(new List<FrameExpression>(), null), new List<AttributedExpression>(),
      new Specification<Expression>(new List<Expression>(), null), body, null, null) {
      EnclosingClass = parent
    };

    checks.Add(checkerMethod);
    parent.Members.Add(checkerMethod);
  }

}

public class VacuityVisitor : TopDownVisitor<AttributedExpression> {
  public List<Expression> ClausesToCheck { get; } = new List<Expression>();

  protected override bool VisitOneExpr(Expression expr, ref AttributedExpression st) {
    switch (expr)
    {
      case BinaryExpr binaryExpr when binaryExpr.Op == BinaryExpr.Opcode.Imp:
        ClausesToCheck.Add(binaryExpr.E0);
        break;
      case ITEExpr iteExpr: // TODO: add support for else-if
        ClausesToCheck.Add(iteExpr.Test);
        break;
    }

    return base.VisitOneExpr(expr, ref st);
  }
}

