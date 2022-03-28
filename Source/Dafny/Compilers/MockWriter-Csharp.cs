using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Dafny; 

/// <summary>
/// Below is the full grammar of ensures clauses that can specify
/// the behavior of an object returned by a mock-annotated method
/// (S is the starting symbol, ID refers to a variable/field/method/type
/// identifier, EXP stands for an arbitrary Dafny expression):
///
/// S       = FORALL
///         | EQUALS 
///         | S && S
/// 
/// EQUALS  = ID.ID (ARGLIST)  == EXP // stubs a method call
///         | ID.ID            == EXP // stubs field access
///         | EQUALS && EQUALS
/// 
/// FORALL  = forall BOUND :: EXP ==> EQUALS
/// 
/// ARGLIST = ID  // this can be one of the bound variables
///         | EXP // this exp may not reference any of the bound variables 
///         | ARG_LIST, ARG_LIST
/// 
/// BOUND   = ID : ID 
///         | BOUND, BOUND
/// 
/// </summary>
public class CsharpMockWriter {

  private readonly CsharpCompiler compiler;
  // maps identifiers to the names of the corresponding mock:
  private Dictionary<IVariable, string> objectToMockName = new();
  // associates a bound variable with the lambda passed to argument matcher
  private Dictionary<IVariable, string> bounds = new();

  public CsharpMockWriter(CsharpCompiler compiler) {
    this.compiler = compiler;
  }

  /// <summary>
  /// Create a body for a method returning a fresh instance of an object 
  /// </summary>
  public ConcreteSyntaxTree CreateFreshMethod(Method method,
    ConcreteSyntaxTree wr) {
    var keywords = CsharpCompiler.Keywords(true, true);
    var returnType = compiler.GetTargetReturnTypeReplacement(method, wr);
    wr.FormatLine($"{keywords}{returnType} {CsharpCompiler.PublicIdProtect(method.CompileName)}() {{");
    // Exploit the fact that compiler creates zero-arguments constructors:
    wr.FormatLine($"return new {returnType}();");
    wr.WriteLine("}");
    return wr;
  }

  /// <summary>
  /// Create a body of a method that mocks one or more objects.
  /// For instance, the following Dafny method:
  /// 
  /// method {:extern} {:mock} CrossReferentialMock()
  ///     returns (e1:Even, e2:Even) 
  ///     ensures fresh(e1) && fresh(e2) 
  ///     ensures e1.Next() == e2
  ///     ensures e2.Next() == e1
  ///
  /// Gets compiled to the following C# code:
  /// (Note that e1Return and e2Return are introduced because e1 and e2
  /// are used inside a lambda and cannot, therefore, be out parameters)
  ///
  /// public static void CrossReferentialMock(out Even e1Return,
  ///                                         out Even e2Return) {
  ///     var e1Mock = new Mock<Even>();
  ///     var e1 = e1Mock.Object;
  ///     var e2Mock = new Mock<Even>();
  ///     var e2 = e2Mock.Object;
  ///     e1Mock.Setup(x => x.Next()).Returns(()=>e2);
  ///     e2Mock.Setup(x => x.Next()).Returns(()=>e1);
  ///     e1Return = e1;
  ///     e2Return = e2;
  /// }
  /// </summary>
  public ConcreteSyntaxTree CreateMockMethod(Method method,
    List<Compiler.TypeArgumentInstantiation> typeArgs, bool createBody,
    ConcreteSyntaxTree wr, bool forBodyInheritance, bool lookasideBody) {

    // The following few lines are identical to those in CreateMethod above:
    var customReceiver = createBody &&
                         !forBodyInheritance &&
                         compiler.NeedsCustomReceiver(method);
    var keywords = CsharpCompiler.Keywords(true, true);
    var returnType = compiler.GetTargetReturnTypeReplacement(method, wr);
    var typeParameters = compiler.TypeParameters(Compiler.TypeArgumentInstantiation.
      ToFormals(compiler.ForTypeParameters(typeArgs, method, lookasideBody)));
    var parameters = compiler
      .GetMethodParameters(method, typeArgs, lookasideBody, customReceiver, returnType);

    // Out parameters cannot be used inside lambda expressions in Csharp
    // but the mocked objects may appear in lambda expressions during
    // mocking (e.g. two objects may cross-reference each other).
    // The solution is to rename the out parameters.
    var parameterString = parameters.ToString();
    var objectToReturnName = method.Outs.ToDictionary(o => o,
      o => compiler.idGenerator.FreshId(o.CompileName + "Return"));
    foreach (var (obj, returnName) in objectToReturnName) {
      parameterString = Regex.Replace(parameterString,
        $"(^|[^a-zA-Z0-9_]){obj.CompileName}([^a-zA-Z0-9_]|$)",
        "$1" + returnName + "$2");
    }
    wr.FormatLine($"{keywords}{returnType} {CsharpCompiler.PublicIdProtect(method.CompileName)}{typeParameters}({parameterString}) {{");

    // Initialize the mocks
    objectToMockName = method.Outs.ToDictionary(o => (IVariable)o,
      o => compiler.idGenerator.FreshId(o.CompileName + "Mock"));
    foreach (var (obj, mockName) in objectToMockName) {
      var typeName = compiler.TypeName(obj.Type, wr, obj.Tok);
      wr.FormatLine($"var {mockName} = new Mock<{typeName}>();");
      wr.FormatLine($"var {obj.CompileName} = {mockName}.Object;");
    }

    // Stub methods and fields according to the Dafny post-conditions:
    foreach (var ensureClause in method.Ens) {
      bounds = new();
      MockExpression(wr, ensureClause.E);
    }

    // Return the mocked objects:
    if (returnType != "void") {
      wr.FormatLine($"return {method.Outs[0].CompileName};");
    } else {
      foreach (var o in method.Outs) {
        wr.FormatLine($"{objectToReturnName[o]} = {o.CompileName};");
      }
    }
    wr.WriteLine("}");
    return wr;
  }

  /// <summary>
  /// If the expression is a bound variable identifier, return the
  /// variable and the string representation of the bounding condition
  /// </summary>
  private Tuple<IVariable, string> GetBound(Expression exp) {
    if (exp is not NameSegment) {
      return null;
    }
    var variable = ((IdentifierExpr)exp.Resolved).Var;
    if (!bounds.ContainsKey(variable)) {
      return null;
    }
    return new Tuple<IVariable, string>(variable, bounds[variable]);
  }

  private void MockExpression(ConcreteSyntaxTree wr, Expression expr) {
    switch (expr) {
      case LiteralExpr literalExpr:
        compiler.TrExpr(literalExpr, wr, false);
        break;
      case ApplySuffix applySuffix:
        MockExpression(wr, applySuffix);
        break;
      case BinaryExpr binaryExpr:
        MockExpression(wr, binaryExpr);
        break;
      case ForallExpr forallExpr:
        MockExpression(wr, forallExpr);
        break;
      case FreshExpr freshExpr:
        break;
      default:
        throw new NotImplementedException();
    }
  }

  private void MockExpression(ConcreteSyntaxTree wr, ApplySuffix applySuffix) {
    var methodApp = (ExprDotName)applySuffix.Lhs;
    var receiver = ((IdentifierExpr)methodApp.Lhs.Resolved).Var;
    var method = ((MemberSelectExpr)methodApp.Resolved).Member.CompileName;
    wr.Format($"{objectToMockName[receiver]}.Setup(x => x.{method}(");

    // The remaining part of the method uses Moq's argument matching to
    // describe the arguments for which the method should be stubbed
    // (in Dafny, this condition is the antecedent over bound variables)
    for (int i = 0; i < applySuffix.Args.Count; i++) {
      var arg = applySuffix.Args[i];
      var bound = GetBound(arg);
      if (bound != null) { // if true, arg is a bound variable
        wr.Write(bound.Item2);
      } else {
        compiler.TrExpr(arg, wr, false);
      }
      if (i != applySuffix.Args.Count - 1) {
        wr.Write(", ");
      }
    }
    wr.Write("))");
  }

  private void MockExpression(ConcreteSyntaxTree wr, BinaryExpr binaryExpr) {
    if (binaryExpr.Op == BinaryExpr.Opcode.And) {
      Dictionary<IVariable, string> oldBounds = bounds
        .ToDictionary(entry => entry.Key, entry => entry.Value);
      MockExpression(wr, binaryExpr.E0);
      bounds = oldBounds;
      MockExpression(wr, binaryExpr.E1);
      return;
    }
    if (binaryExpr.Op != BinaryExpr.Opcode.Eq) {
      throw new NotImplementedException();
    }
    if (binaryExpr.E0 is ExprDotName exprDotName) { // field stubbing
      var obj = ((IdentifierExpr)exprDotName.Lhs.Resolved).Var;
      var field = ((MemberSelectExpr)exprDotName.Resolved).Member.CompileName;
      wr.Format($"{objectToMockName[obj]}.SetupGet({obj.CompileName} => {obj.CompileName}.@{field}).Returns( ");
      compiler.TrExpr(binaryExpr.E1, wr, false);
      wr.WriteLine(");");
      return;
    }
    if (binaryExpr.E0 is not ApplySuffix applySuffix) {
      throw new NotImplementedException();
    }
    MockExpression(wr, applySuffix);
    wr.Write(".Returns(");
    wr.Write("(");
    for (int i = 0; i < applySuffix.Args.Count; i++) {
      var arg = applySuffix.Args[i];
      var typeName = compiler.TypeName(arg.Type, wr, arg.tok);
      var bound = GetBound(arg);
      if (bound != null) {
        wr.Format($"{typeName} {bound.Item1.CompileName}");
      } else {
        // if the argument is not a bound variable, it is irrelevant to the
        // expression in the lambda
        wr.Format($"{typeName} _");
      }
      if (i != applySuffix.Args.Count - 1) {
        wr.Write(", ");
      }
    }
    wr.Write(")=>");
    compiler.TrExpr(binaryExpr.E1, wr, false);
    wr.WriteLine(");");
  }

  private void MockExpression(ConcreteSyntaxTree wr, ForallExpr forallExpr) {
    if (forallExpr.Term is not BinaryExpr binaryExpr) {
      throw new NotImplementedException();
    }
    var declarations = new List<string>();

    // a MultiMatcher is created to convert an antecedent of the implication
    // following the forall statement to argument matching calls in Moq
    var matcherName = compiler.idGenerator.FreshId("matcher");

    var tmpId = compiler.idGenerator.FreshId("tmp");
    for (int i = 0; i < forallExpr.BoundVars.Count; i++) {
      var boundVar = forallExpr.BoundVars[i];
      var varType = compiler.TypeName(boundVar.Type, wr, boundVar.tok);
      bounds[boundVar] = $"It.Is<{varType}>(x => {matcherName}.Match(x))";
      declarations.Add($"var {boundVar.CompileName} = ({varType}) {tmpId}[{i}];");
    }

    wr.WriteLine($"var {matcherName} = new Dafny.MultiMatcher({declarations.Count}, {tmpId} => {{");
    foreach (var declaration in declarations) {
      wr.WriteLine($"\t{declaration}");
    }

    switch (binaryExpr.Op) {
      case BinaryExpr.Opcode.Imp:
        wr.Write("\treturn ");
        compiler.TrExpr(binaryExpr.E0, wr, false);
        wr.WriteLine(";");
        binaryExpr = (BinaryExpr)binaryExpr.E1;
        break;
      case BinaryExpr.Opcode.Eq:
        wr.WriteLine("\treturn true;");
        break;
      default:
        throw new NotImplementedException();
    }
    wr.WriteLine("});");
    MockExpression(wr, binaryExpr);
  }

  /// <summary>
  /// Adds MultiMatcher class to the specified ConcreteSyntaxTree.
  /// MultiMatcher allows converting one expression over many arguments
  /// (like ones one finds in Dafny in antecedent of a forall statement)
  /// to many separate predicates over each argument (which is how argument
  /// matching is done in expressionC#'s Moq library)
  /// So, for instance, a Dafny postcondition
  ///   forall a,b:int :: a > b ==> o.m(a, b) == 4
  /// is converted to:
  /// 
  ///   var matcher = new Dafny.MultiMatcher(2, args => {
  ///     return args[0] > args[1];
  ///   });
  ///   o.Setup(x => x.m(It.Is<int>(a => matcher.Match(a)),
  ///                    It.Is<int>(b => matcher.Match(b)))).Returns(4);
  /// 
  /// </summary>
  internal static void EmitMultiMatcher(ConcreteSyntaxTree dafnyNamespace) {
    const string multiMatcher = @"
      class MultiMatcher {

        private readonly Func<object[], bool> predicate;
        private readonly int argumentCount;
        private readonly List<object> collectedArguments;

        public MultiMatcher(int argumentCount, Func<object[], bool> predicate) {
          this.predicate = predicate;
          this.argumentCount = argumentCount;
          collectedArguments = new();
        }

        public bool Match(object argument) {
          collectedArguments.Add(argument);
          if (collectedArguments.Count != argumentCount) {
            return true;
          }
          bool result = predicate(collectedArguments.ToArray());
          collectedArguments.Clear();
          return result;
        }
      }";
    var memberDeclaration = SyntaxFactory.ParseMemberDeclaration(multiMatcher);
    dafnyNamespace.WriteLine(memberDeclaration.ToFullString());
  }

  internal static void EmitImports(ConcreteSyntaxTree wr) {
    wr.WriteLine("using Moq;");
    wr.WriteLine("using System.Collections.Generic;");
  }
}