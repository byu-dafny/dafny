using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Policy;
using Microsoft.Boogie;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.Dafny; 

public class ContractIntegrity : IRewriter {
  
  private readonly List<Method> checks = new();
  private ErrorReporter reporter;
  private readonly VacuityVisitor vacuityVisitor = new();
  private readonly OutputVisitor outputVisitor = new();
  private readonly RedundancyVisitor redundancyVisitor = new();
  private static Dictionary<int, string> lineNumberToAssertion = new Dictionary<int, string>();
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
    List<(TopLevelDeclWithMembers, MemberDecl)> membersToCheck = (from topLevelDecl in m.TopLevelDecls.OfType<TopLevelDeclWithMembers>() from decl in topLevelDecl.Members where ShouldGenerateChecker(decl) select (topLevelDecl, decl)).ToList();

    // Find module members to check.

    // Generate a check for each of the methods identified above.
    foreach (var (topLevelDecl, decl) in membersToCheck) {
      GenerateMethodCheck((Method) decl, topLevelDecl, "delimcontradiction_requiresdelim"); // precondition contradiction check
      GenerateMethodCheck((Method) decl, topLevelDecl, "delimcontradiction_ensuresdelim"); // postcondition contradiction check
      GenerateMethodCheck((Method) decl, topLevelDecl, "delimvacuity_requiresdelim"); // precondition vacuity check
      GenerateMethodCheck((Method) decl, topLevelDecl, "delimvacuity_ensuresdelim"); // postcondition vacuity check
      GenerateMethodCheck((Method) decl, topLevelDecl, "delimunconstraineddelim"); // unconstrained output check
      GenerateMethodCheck((Method) decl, topLevelDecl, "delimredundancy_requiresdelim"); // precondition redundancy check
      GenerateMethodCheck((Method) decl, topLevelDecl, "delimredundancy_ensuresdelim"); // postcondition redundancy check
    }
  }

  // Creates an assert statement that checks for contradiction
  private static Statement CreateVacuityAssertStatement(AttributedExpression expr) {
    return new AssertStmt(expr.RangeToken, expr.E, null, null, new Attributes("subsumption 0", new List<Expression>(), null));
  }
  
  // Creates a conjunction of provided expressions, negates the conjunction, creates an assertion
  private static Statement ConjunctAndNegate(IEnumerable<AttributedExpression> constraints) {
    // no constraints - assert false
    if (constraints == null || !constraints.Any()) {
      return new AssertStmt(RangeToken.NoToken, Expression.CreateBoolLiteral(null, false), null, null,
        new Attributes("subsumption 0", new List<Expression>(), null));
    }
    
    var combined = Expression.CreateIntLiteral(null, 0);
    var i = 0;
    foreach (var constraint in constraints) {
      // constraint.E.Type = Type.Bool;
      // combined = i == 0 ? Expression.CreateParensExpression(constraint.E.tok, constraint.E) : Expression.CreateAnd(combined, Expression.CreateParensExpression(constraint.E.tok, constraint.E));
      combined = i == 0 ? constraint.E : Expression.CreateAnd(combined, constraint.E);
      i = 1;
    }

    var exprToCheck = Expression.CreateNot(combined.tok, combined);
    return new AssertStmt(exprToCheck.RangeToken, exprToCheck, null, null, new Attributes("subsumption 0", new List<Expression>(), null));
  }
  
  // Creates a conjunction of provided attributed expressions
  private static Expression Conjunct(IEnumerable<AttributedExpression> constraints) {
    Expression conjunction = Expression.CreateBoolLiteral(null, true);
    var i = 0;
    foreach (var constraint in constraints) {
      // constraint.E.Type = Type.Bool;
      // conjunction = i == 0 ? Expression.CreateParensExpression(constraint.E.tok, constraint.E) : Expression.CreateAnd(conjunction, Expression.CreateParensExpression(constraint.E.tok, constraint.E));
      conjunction = i == 0 ? constraint.E : Expression.CreateAnd(conjunction, constraint.E);
      i = 1;
    }
    
    return conjunction;
  }
  
  // Creates a conjunction of provided expressions
  private static Expression ConjunctE(IEnumerable<Expression> constraints) {
    Expression conjunction = Expression.CreateBoolLiteral(new Token(0, 0), true);
    var i = 0;
    foreach (var constraint in constraints) {
      conjunction = i == 0 ? Expression.CreateParensExpression(constraint.tok, constraint) : Expression.CreateAnd(conjunction, Expression.CreateParensExpression(constraint.tok, constraint));
      i = 1;
    }
    
    return conjunction;
  }
  
  // Creates a disjunction of provided expressions
  private static Expression Disjunct(List<Expression> constraints) {
    Expression disjunction = null;
    var i = 0;
    foreach (var constraint in constraints) {
      // constraint.Type = Type.Bool;
      // disjunction = i == 0 ? Expression.CreateParensExpression(constraint.tok, constraint) : Expression.CreateOr(disjunction, Expression.CreateParensExpression(constraint.tok, constraint));
      disjunction = i == 0 ? constraint : Expression.CreateOr(disjunction, constraint);
      i = 1;
    }
    
    return disjunction;
  }
  
  // Creates an assert statement for unconstrained output
  private Statement UnconstrainedAssertion(List<Expression> conditions, IEnumerable<AttributedExpression> requires) {
    var requiresConjunction = Conjunct(requires);
    var conditionsDisjunction = Disjunct(conditions);
    var finalExpr = Expression.CreateImplies(requiresConjunction, conditionsDisjunction);
    return new AssertStmt(finalExpr.RangeToken, finalExpr, null, null, new Attributes("subsumption 0", new List<Expression>(), null));
  }
  
  // Compiles all the asserts needed for a contradiction check and inserts them into the body
  private  List<Tuple<string,BlockStmt>> MakeContradictionCheckingBody(IEnumerable<AttributedExpression> constraints, string type) {
    var assertStmt = ConjunctAndNegate(constraints); // TODO: determine where the contradiction happens
    List<Tuple<string, BlockStmt>> blocks = new List<Tuple<string, BlockStmt>>();
    var name = "all " + type + " clauses" + "delim" + constraints.First().tok.line;
    blocks.Add(new Tuple<string, BlockStmt>(name, new BlockStmt(RangeToken.NoToken, new List<Statement> { assertStmt })));
    return blocks;
  }

  // Compiles all the asserts needed for a vacuity check and inserts them into the body
  private List<Tuple<string, BlockStmt>> MakeVacuityCheckingBody(IEnumerable<AttributedExpression> constraints, string type) {
    
    // TODO: The commented code checked the LHS of an implication, or the test of an if-statement, for 
    // TODO: a contradiction. We don't need to do this to detect vacuity - any clause that is always 
    // TODO: true is a case of vacuity. We may want this code in the future to detect the cause of the vacuity
    // TODO: for example, is it because of a contradiction on the LHS, or is the clause just true?
    // var toTest = new List<Expression>();
    // foreach (var constraint in constraints)
    // {
    //   vacuityVisitor.Visit(constraint.E, constraint);
    // }
    //
    // toTest.AddRange(vacuityVisitor.ClausesToCheck);
    // vacuityVisitor.ClausesToCheck = new List<Expression>(); 
    
    // TODO: as of now, ite expressions are not handled - add them in maybe
    List<Tuple<string, BlockStmt>> blocks = new List<Tuple<string, BlockStmt>>();
    
    var assertStmts = constraints.Select(CreateVacuityAssertStatement);
    foreach (var stmt in assertStmts) {
      var name = type + " " + stmt.SubExpressions.First() + "delim" + stmt.Tok.line;
      blocks.Add(new Tuple<string, BlockStmt>(name, new BlockStmt(RangeToken.NoToken, new List<Statement> { stmt })));
    }

    return blocks;
  }

  private static IEnumerable<AttributedExpression> FilterEnsures(IEnumerable<AttributedExpression> ensures, string outputName) {
    return (from expr in ensures where expr.E.ToString().Contains(outputName) select expr).ToList();
  }
  
  // Compiles all the asserts needed for a unconstrained output check and inserts them into the body
  private List<Tuple<string, BlockStmt>> MakeOutputCheckingBody(IEnumerable<AttributedExpression> requires, IEnumerable<AttributedExpression> ensures, IEnumerable<Formal> outputs, int line) {
    var outputChecks = new List<Tuple<string, BlockStmt>>();

    // loop through outputs and generate check for each
    foreach (var output in outputs) {
      var outputStr = output.Name; // extract the name of the output
      var mentioned = FilterEnsures(ensures, outputStr);

      if (!mentioned.Any()) {
        var bs = new BlockStmt(RangeToken.NoToken, new List<Statement> { new AssertStmt(RangeToken.NoToken, Expression.CreateBoolLiteral(output.tok, false), null, null, new Attributes("subsumption 0", new List<Expression>(), null)) });
        var namee = "output: " + output.Name + "delim" + line;
        outputChecks.Add(new Tuple<string, BlockStmt>(namee, bs));
        continue;
      }

      foreach (var clause in mentioned) {
        outputVisitor.Visit(clause.E, clause);  
      }

      var conditionalExpressions = outputVisitor.ClausesToCheck;
      var nonConditionalExpressions = outputVisitor.ClausesWithNoConditionals;
      
      outputVisitor.ClausesToCheck = new List<Expression>();
      outputVisitor.ClausesWithNoConditionals = new List<Expression>();

      if (nonConditionalExpressions.Any()) {
        var bs = new BlockStmt(RangeToken.NoToken, new List<Statement> { new AssertStmt(RangeToken.NoToken, Expression.CreateBoolLiteral(null, true), null, null, new Attributes("subsumption 0", new List<Expression>(), null)) });
        var namee = "output: " + output.Name + "delim" + line;
        outputChecks.Add(new Tuple<string, BlockStmt>(namee, bs));
        continue;
      }
      var blockStmt = new BlockStmt(RangeToken.NoToken, new List<Statement> { UnconstrainedAssertion(conditionalExpressions, requires) });
      var name = "output: " + output.Name + "delim" + line;
      outputChecks.Add(new Tuple<string, BlockStmt>(name, blockStmt));
    }
    
    return outputChecks;
  }
  
  private List<Tuple<string, BlockStmt>> MakeRedundancyCheckingBody(List<AttributedExpression> constraints, string type) {
    
    var blocks = new List<Tuple<string, BlockStmt>>();
    
    // split clauses by &&
    var clauses = new List<Expression>();
    
    foreach (var clause in constraints) {
      if (clause.E is BinaryExpr binaryExpr && binaryExpr.Op == BinaryExpr.Opcode.And) {
        redundancyVisitor.Visit(clause.E, clause); 
      } else {
        clauses.Add(clause.E);
      } 
    }
    
    clauses.AddRange(redundancyVisitor.ClausesToCheck);
    redundancyVisitor.ClausesToCheck = new List<Expression>();
    
    // create assertions
    foreach (var clause in clauses) {
      var filteredClauses = clauses.Where(c => c != clause);
      var conjunctedClauses = ConjunctE(filteredClauses);
      var assertionExpr = Expression.CreateImplies(conjunctedClauses, clause);
      var blockStmt =  new BlockStmt(RangeToken.NoToken, new List<Statement> { new AssertStmt(assertionExpr.RangeToken, assertionExpr, null, null,
        new Attributes("subsumption 0", new List<Expression>(), null)) });
      var name = type + " " + clause + "delim" + clause.Tok.line;
      blocks.Add(new Tuple<string, BlockStmt>(name, blockStmt));
    }

    return blocks;

  }
  
  // Creates a method that checks the provided method (decl) for the specified issue (checkName)
  private void GenerateMethodCheck(Method decl, TopLevelDeclWithMembers parent, string checkName) {
    var bodies = checkName switch {
      "delimcontradiction_requiresdelim" => (decl.Req.Count > 0 ? MakeContradictionCheckingBody(decl.Req, "requires") : new List<Tuple<string, BlockStmt>>()),
      "delimcontradiction_ensuresdelim" => (decl.Ens.Count > 0 ? MakeContradictionCheckingBody(decl.Ens, "ensures") : new List<Tuple<string, BlockStmt>>()),
      "delimvacuity_requiresdelim" => (decl.Req.Count > 0 ? MakeVacuityCheckingBody(decl.Req, "requires") : new List<Tuple<string, BlockStmt>>()),
      "delimvacuity_ensuresdelim" => (decl.Ens.Count > 0 ? MakeVacuityCheckingBody(decl.Ens, "ensures") : new List<Tuple<string, BlockStmt>>()),
      "delimunconstraineddelim" => (decl.Outs.Count > 0 ? MakeOutputCheckingBody(decl.Req,decl.Ens, decl.Outs, decl.StartToken.line) : new List<Tuple<string, BlockStmt>>()),
      "delimredundancy_requiresdelim" => (decl.Req.Count > 0 ? MakeRedundancyCheckingBody(decl.Req, "requires") : new List<Tuple<string, BlockStmt>>()),
      "delimredundancy_ensuresdelim" => (decl.Ens.Count > 0 ? MakeRedundancyCheckingBody(decl.Ens, "ensures") : new List<Tuple<string, BlockStmt>>()),
      _ => new List<Tuple<string, BlockStmt>>()
    };

    // inputs to each check for the given method
    var insFromIns = decl.Ins;
    var insFromOuts = decl.Outs;
    var ins = insFromIns.Concat(insFromOuts).ToList();
    
    
    foreach (var body in bodies) {
      var checkerMethod = new Method(RangeToken.NoToken, new Name(body.Item1 + checkName + decl.Name), false, false,
        new List<TypeParameter>(),
        ins, new List<Formal>(), new List<AttributedExpression>(),
        new Specification<FrameExpression>(new List<FrameExpression>(), null), new List<AttributedExpression>(),
        new Specification<Expression>(new List<Expression>(), null), body.Item2, null, null) {
        EnclosingClass = parent
      };
      checks.Add(checkerMethod);
      parent.Members.Add(checkerMethod);
    }
  }
}

// TODO: not currently used
// Visitor that extracts expressions to check for vacuity
public class VacuityVisitor : TopDownVisitor<AttributedExpression> {
  public List<Expression> ClausesToCheck { get; set; } = new();
  // public bool flag = false;
  public Stack<ForallExpr> ForallExprs = new Stack<ForallExpr>();

  protected override bool VisitOneExpr(Expression expr, ref AttributedExpression st) {
    switch (expr)
    {
      case ForallExpr forallExpr:
        // Expression.CreateQuantifier(forallExpr, true);
        ForallExprs.Push(forallExpr);
        VisitOneExpr(forallExpr.Term, ref st);
        return false;
      case BinaryExpr { Op: BinaryExpr.Opcode.Imp } binaryExpr:
        if (ForallExprs.TryPop(out var faExp)) {
          var bin = ((ChainingExpression)binaryExpr.E0).E;
          var negation = Expression.CreateNot(bin.tok, bin);
          ClausesToCheck.Add(Expression.CreateQuantifier(faExp, true, negation));
          return false;
        }
        ClausesToCheck.Add(Expression.CreateNot(binaryExpr.E0.tok, binaryExpr.E0));
        break;
      case ITEExpr iteExpr: // TODO: add support for else-if
        ClausesToCheck.Add(iteExpr.Test);
        break;
    }

    return base.VisitOneExpr(expr, ref st);
  }
}

// Visitor that extracts expressions to check for unconstrained output
public class OutputVisitor : TopDownVisitor<AttributedExpression> {
  public List<Expression> ClausesToCheck { get; set; } = new();
  public List<Expression> ClausesWithNoConditionals { get; set; } = new();

  protected override bool VisitOneExpr(Expression expr, ref AttributedExpression st) {
    switch (expr)
    {
      case BinaryExpr { Op: BinaryExpr.Opcode.Imp } binaryExpr:
        ClausesToCheck.Add(binaryExpr.E0);
        return false;
      case ITEExpr iteExpr: // TODO: add support for else-if
        ClausesToCheck.Add(iteExpr.Test);
        return false;
      case { } and not LiteralExpr and not NameSegment:
        ClausesWithNoConditionals.Add(expr);
        break;
    }

    return base.VisitOneExpr(expr, ref st);
  }
}

// Visitor that extracts expressions to check for redundancy
public class RedundancyVisitor : TopDownVisitor<AttributedExpression> {
  public List<Expression> ClausesToCheck { get; set; } = new();

  protected override bool VisitOneExpr(Expression expr, ref AttributedExpression st) {
    switch (expr)
    {
      case BinaryExpr { Op: BinaryExpr.Opcode.And } binaryExpr:
        ClausesToCheck.Add(binaryExpr.E0);
        ClausesToCheck.Add(binaryExpr.E1);
        break;
    }
    return base.VisitOneExpr(expr, ref st);
  }
}

