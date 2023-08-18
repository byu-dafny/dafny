using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;

namespace Microsoft.Dafny; 

public class ContractIntegrity : IRewriter {
  
  private readonly List<Method> checks = new();
  private ErrorReporter reporter;
  private readonly VacuityVisitor visitor = new();
  
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
      GenerateMethodCheck((Method) decl, topLevelDecl, "_contradiction_requires"); // precondition contradiction check
      // GenerateMethodCheck((Method) decl, topLevelDecl, "_contradiction_ensures"); // postcondition contradiction check
      // GenerateMethodCheck((Method) decl, topLevelDecl, "_vacuity_requires"); // precondition vacuity check
      // GenerateMethodCheck((Method) decl, topLevelDecl, "_vacuity_ensures"); // postcondition vacuity check
      // GenerateMethodCheck((Method) decl, topLevelDecl, "_unconstrained"); // unconstrained output check
    }
  }
  
  // Creates an assert statement that checks for contradiction
  private Statement CreateContradictionAssertStatement(Expression expr) {
    var tok = expr.tok;
    var exprToCheck = Expression.CreateNot(tok, expr);
    return new AssertStmt(expr.RangeToken, expr, null, null, new Attributes("subsumption 0", new List<Expression>(), null));
  }
  
  // Combines the assert statements and negates the combination
  private Statement CombineAndNegate(IEnumerable<AttributedExpression> constraints) {
    Expression combined = null;
    var i = 0;
    foreach (var constraint in constraints) {
      combined = i == 0 ? constraint.E : Expression.CreateAnd(combined, constraint.E);
      i = 1;
    }
    var tok = combined.tok;
    var exprToCheck = Expression.CreateNot(tok, combined);
    return new AssertStmt(exprToCheck.RangeToken, exprToCheck, null, null, new Attributes("subsumption 0", new List<Expression>(), null));
  }
  
  // Compiles all the asserts needed for a contradiction check and inserts them into the body
  private BlockStmt MakeContradictionCheckingBody(IEnumerable<AttributedExpression> constraints) {
    var assertStmts = new List<Statement>();
    // foreach (var constraint in constraints) {
    //   assertStmts.Add(CreateContradictionAssertStatement(constraint.E));
    // }
    var assertStmt = CombineAndNegate(constraints);
    assertStmts.Add(assertStmt);
    return new BlockStmt(RangeToken.NoToken, assertStmts);
  }

  // Compiles all the asserts needed for a vacuity check and inserts them into the body
  private BlockStmt MakeVacuityCheckingBody(IEnumerable<AttributedExpression> constraints) {

    List<Expression> toTest = new List<Expression>();
    foreach (var constraint in constraints)
    {
      visitor.Visit(constraint.E, constraint);
    }
    
    toTest.AddRange(visitor.ClausesToCheck);
    visitor.ClausesToCheck = new List<Expression>();
    
    var assertStmts = toTest.Select(CreateContradictionAssertStatement);
    return new BlockStmt(RangeToken.NoToken, assertStmts.ToList());
  }
  
  // Compiles all the asserts needed for a contradiction check and inserts them into the body
  // private BlockStmt MakeOutputCheckingBody(IEnumerable<AttributedExpression> requires, IEnumerable<AttributedExpression> ensures) {
  //   var assertStmts = new List<Statement>();
  //   foreach (var constraint in constraints) {
  //     assertStmts.Add(CreateContradictionAssertStatement(constraint.E));
  //   }
  //   return new BlockStmt(RangeToken.NoToken, assertStmts);
  // }
  
  // Creates a method that checks the provided method (decl) for the specified issue (checkName)
  private void GenerateMethodCheck(Method decl, TopLevelDeclWithMembers parent, string checkName) {
    BlockStmt body = checkName switch {
      "_contradiction_requires" => MakeContradictionCheckingBody(decl.Req),
      "_contradiction_ensures" => MakeContradictionCheckingBody(decl.Ens),
      "_vacuity_requires" => MakeVacuityCheckingBody(decl.Req),
      "_vacuity_ensures" => MakeVacuityCheckingBody(decl.Ens),
      // "_unconstrained" => MakeOutputCheckingBody(decl.Req, decl.Ens),
      _ => null
    };

    var name = decl.Name + checkName;
    
    // inputs to each check for the given method
    var insFromIns = decl.Ins;
    var insFromOuts = decl.Outs;
    var ins = insFromIns.Concat(insFromOuts).ToList();

    var checkerMethod = new Method(RangeToken.NoToken, new Name(name), false, false, new List<TypeParameter>(),
      ins, new List<Formal>(), new List<AttributedExpression>(), new Specification<FrameExpression>(new List<FrameExpression>(), null), new List<AttributedExpression>(),
      new Specification<Expression>(new List<Expression>(), null), body, null, null) {
      EnclosingClass = parent
    };

    checks.Add(checkerMethod);
    parent.Members.Add(checkerMethod);
  }

}

// Visitor that extracts expressions to check for vacuity
public class VacuityVisitor : TopDownVisitor<AttributedExpression> {
  public List<Expression> ClausesToCheck { get; set; } = new();

  protected override bool VisitOneExpr(Expression expr, ref AttributedExpression st) {
    switch (expr)
    {
      case BinaryExpr { Op: BinaryExpr.Opcode.Imp } binaryExpr:
        ClausesToCheck.Add(binaryExpr.E0);
        break;
      case ITEExpr iteExpr: // TODO: add support for else-if
        ClausesToCheck.Add(iteExpr.Test);
        break;
    }

    return base.VisitOneExpr(expr, ref st);
  }
}

