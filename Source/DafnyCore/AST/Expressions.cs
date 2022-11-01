using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Numerics;
using System.Linq;
using System.Diagnostics;
using Microsoft.Boogie;

namespace Microsoft.Dafny;

[DebuggerDisplay("{Printer.ExprToString(this)}")]
public abstract class Expression : INode {
  public readonly IToken tok;
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(tok != null);
  }

  [Pure]
  public bool WasResolved() {
    return Type != null;
  }

  public Expression Resolved {
    get {
      Contract.Requires(WasResolved());  // should be called only on resolved expressions; this approximates that precondition
      Expression r = this;
      while (true) {
        Contract.Assert(r.WasResolved());  // this.WasResolved() implies anything it reaches is also resolved
        var rr = r as ConcreteSyntaxExpression;
        if (rr == null) {
          return r;
        }
        r = rr.ResolvedExpression;
        if (r == null) {
          // for a NegationExpression, we're willing to return its non-ResolveExpression form (since it is filled in
          // during a resolution phase after type checking and we may be called here during type checking)
          return rr is NegationExpression ? rr : null;
        }
      }
    }
  }

  [FilledInDuringResolution] protected Type type;
  public Type Type {
    get {
      Contract.Ensures(type != null || Contract.Result<Type>() == null);  // useful in conjunction with postcondition of constructor
      return type == null ? null : type.Normalize();
    }
    set {
      Contract.Requires(!WasResolved());  // set it only once
      Contract.Requires(value != null);

      //modifies type;
      type = value.Normalize();
    }
  }
  /// <summary>
  /// This method can be used when .Type has been found to be erroneous and its current value
  /// would be unexpected by the rest of the resolver. This method then sets .Type to a neutral
  /// value.
  /// </summary>
  public void ResetTypeAssignment() {
    Contract.Requires(WasResolved());
    type = new InferredTypeProxy();
  }
#if TEST_TYPE_SYNONYM_TRANSPARENCY
    public void DebugTest_ChangeType(Type ty) {
      Contract.Requires(WasResolved());  // we're here to set it again
      Contract.Requires(ty != null);
      type = ty;
    }
#endif

  public Expression(IToken tok) {
    Contract.Requires(tok != null);
    Contract.Ensures(type == null);  // we would have liked to have written Type==null, but that's not admissible or provable

    this.tok = tok;
  }

  /// <summary>
  /// Returns the non-null subexpressions of the Expression.  To be called after the expression has been resolved; this
  /// means, for example, that any concrete syntax that resolves to some other expression will return the subexpressions
  /// of the resolved expression.
  /// </summary>
  public virtual IEnumerable<Expression> SubExpressions {
    get { yield break; }
  }

  private RangeToken rangeToken = null;

  // Contains tokens that did not make it in the AST but are part of the expression,
  // Enables ranges to be correct.
  protected IToken[] FormatTokens = null;

  /// Creates a token on the entire range of the expression.
  /// Used only for error reporting.
  public virtual RangeToken RangeToken {
    get {
      if (rangeToken == null) {
        if (tok is RangeToken tokAsRange) {
          rangeToken = tokAsRange;
        } else {
          var startTok = tok;
          var endTok = tok;
          foreach (var e in SubExpressions) {
            if (e.tok.Filename != tok.Filename || e.IsImplicit) {
              // Ignore auto-generated expressions, if any.
              continue;
            }

            if (e.StartToken.pos < startTok.pos) {
              startTok = e.StartToken;
            } else if (e.EndToken.pos > endTok.pos) {
              endTok = e.EndToken;
            }
          }

          if (FormatTokens != null) {
            foreach (var token in FormatTokens) {
              if (token.Filename != tok.Filename) {
                continue;
              }

              if (token.pos < startTok.pos) {
                startTok = token;
              }

              if (token.pos + token.val.Length > endTok.pos + endTok.val.Length) {
                endTok = token;
              }
            }
          }

          rangeToken = new RangeToken(startTok, endTok);
        }
      }

      return rangeToken;
    }
  }

  public IToken StartToken => RangeToken.StartToken;
  public IToken EndToken => RangeToken.EndToken;

  /// <summary>
  /// Returns the list of types that appear in this expression proper (that is, not including types that
  /// may appear in subexpressions). Types occurring in sub-statements of the expression are not included.
  /// To be called after the expression has been resolved.
  /// </summary>
  public virtual IEnumerable<Type> ComponentTypes {
    get { yield break; }
  }

  public virtual bool IsImplicit {
    get { return false; }
  }

  public static IEnumerable<Expression> Conjuncts(Expression expr) {
    Contract.Requires(expr != null);
    Contract.Requires(expr.Type.IsBoolType);
    Contract.Ensures(cce.NonNullElements(Contract.Result<IEnumerable<Expression>>()));

    expr = StripParens(expr);
    if (expr is UnaryOpExpr unary && unary.Op == UnaryOpExpr.Opcode.Not) {
      foreach (Expression e in Disjuncts(unary.E)) {
        yield return Expression.CreateNot(e.tok, e);
      }
      yield break;

    } else if (expr is BinaryExpr bin) {
      if (bin.ResolvedOp == BinaryExpr.ResolvedOpcode.And) {
        foreach (Expression e in Conjuncts(bin.E0)) {
          yield return e;
        }
        foreach (Expression e in Conjuncts(bin.E1)) {
          yield return e;
        }
        yield break;
      }
    }

    yield return expr;
  }

  public static IEnumerable<Expression> Disjuncts(Expression expr) {
    Contract.Requires(expr != null);
    Contract.Requires(expr.Type.IsBoolType);
    Contract.Ensures(cce.NonNullElements(Contract.Result<IEnumerable<Expression>>()));

    expr = StripParens(expr);
    if (expr is UnaryOpExpr unary && unary.Op == UnaryOpExpr.Opcode.Not) {
      foreach (Expression e in Conjuncts(unary.E)) {
        yield return Expression.CreateNot(e.tok, e);
      }
      yield break;

    } else if (expr is BinaryExpr bin) {
      if (bin.ResolvedOp == BinaryExpr.ResolvedOpcode.Or) {
        foreach (Expression e in Conjuncts(bin.E0)) {
          yield return e;
        }
        foreach (Expression e in Conjuncts(bin.E1)) {
          yield return e;
        }
        yield break;
      } else if (bin.ResolvedOp == BinaryExpr.ResolvedOpcode.Imp) {
        foreach (Expression e in Conjuncts(bin.E0)) {
          yield return Expression.CreateNot(e.tok, e);
        }
        foreach (Expression e in Conjuncts(bin.E1)) {
          yield return e;
        }
        yield break;
      }
    }

    yield return expr;
  }

  /// <summary>
  /// Create a resolved expression of the form "e0 + e1"
  /// </summary>
  public static Expression CreateAdd(Expression e0, Expression e1) {
    Contract.Requires(e0 != null);
    Contract.Requires(e1 != null);
    Contract.Requires(
      (e0.Type.IsNumericBased(Type.NumericPersuasion.Int) && e1.Type.IsNumericBased(Type.NumericPersuasion.Int)) ||
      (e0.Type.IsNumericBased(Type.NumericPersuasion.Real) && e1.Type.IsNumericBased(Type.NumericPersuasion.Real)));
    Contract.Ensures(Contract.Result<Expression>() != null);
    var s = new BinaryExpr(e0.tok, BinaryExpr.Opcode.Add, e0, e1);
    s.ResolvedOp = BinaryExpr.ResolvedOpcode.Add;  // resolve here
    s.Type = e0.Type.NormalizeExpand();  // resolve here
    return s;
  }

  /// <summary>
  /// Create a resolved expression of the form "e0 * e1"
  /// </summary>
  public static Expression CreateMul(Expression e0, Expression e1) {
    Contract.Requires(e0 != null);
    Contract.Requires(e1 != null);
    Contract.Requires(
      (e0.Type.IsNumericBased(Type.NumericPersuasion.Int) && e1.Type.IsNumericBased(Type.NumericPersuasion.Int)) ||
      (e0.Type.IsNumericBased(Type.NumericPersuasion.Real) && e1.Type.IsNumericBased(Type.NumericPersuasion.Real)));
    Contract.Ensures(Contract.Result<Expression>() != null);
    var s = new BinaryExpr(e0.tok, BinaryExpr.Opcode.Mul, e0, e1);
    s.ResolvedOp = BinaryExpr.ResolvedOpcode.Mul;  // resolve here
    s.Type = e0.Type.NormalizeExpand();  // resolve here
    return s;
  }

  /// <summary>
  /// Create a resolved expression of the form "CVT(e0) - CVT(e1)", where "CVT" is either "int" (if
  /// e0.Type is an integer-based numeric type) or "real" (if e0.Type is a real-based numeric type).
  /// </summary>
  public static Expression CreateSubtract_TypeConvert(Expression e0, Expression e1) {
    Contract.Requires(e0 != null);
    Contract.Requires(e1 != null);
    Contract.Requires(
      (e0.Type.IsNumericBased(Type.NumericPersuasion.Int) && e1.Type.IsNumericBased(Type.NumericPersuasion.Int)) ||
      (e0.Type.IsNumericBased(Type.NumericPersuasion.Real) && e1.Type.IsNumericBased(Type.NumericPersuasion.Real)) ||
      (e0.Type.IsBitVectorType && e1.Type.IsBitVectorType) ||
      (e0.Type.IsCharType && e1.Type.IsCharType));
    Contract.Ensures(Contract.Result<Expression>() != null);

    Type toType;
    if (e0.Type.IsNumericBased(Type.NumericPersuasion.Int)) {
      toType = Type.Int;
    } else if (e0.Type.IsNumericBased(Type.NumericPersuasion.Real)) {
      toType = Type.Real;
    } else {
      Contract.Assert(e0.Type.IsBitVectorType || e0.Type.IsCharType);
      toType = Type.Int; // convert char and bitvectors to int
    }
    e0 = CastIfNeeded(e0, toType);
    e1 = CastIfNeeded(e1, toType);
    return CreateSubtract(e0, e1);
  }

  private static Expression CastIfNeeded(Expression expr, Type toType) {
    if (!expr.Type.Equals(toType)) {
      var cast = new ConversionExpr(expr.tok, expr, toType);
      cast.Type = toType;
      return cast;
    } else {
      return expr;
    }
  }

  /// <summary>
  /// Create a resolved expression of the form "e0 - e1"
  /// </summary>
  public static Expression CreateSubtract(Expression e0, Expression e1) {
    Contract.Requires(e0 != null);
    Contract.Requires(e0.Type != null);
    Contract.Requires(e1 != null);
    Contract.Requires(e1.Type != null);
    Contract.Requires(
      (e0.Type.IsNumericBased(Type.NumericPersuasion.Int) && e1.Type.IsNumericBased(Type.NumericPersuasion.Int)) ||
      (e0.Type.IsNumericBased(Type.NumericPersuasion.Real) && e1.Type.IsNumericBased(Type.NumericPersuasion.Real)) ||
      (e0.Type.IsBigOrdinalType && e1.Type.IsBigOrdinalType));
    Contract.Ensures(Contract.Result<Expression>() != null);
    var s = new BinaryExpr(e0.tok, BinaryExpr.Opcode.Sub, e0, e1);
    s.ResolvedOp = BinaryExpr.ResolvedOpcode.Sub;  // resolve here
    s.Type = e0.Type.NormalizeExpand();  // resolve here (and it's important to remove any constraints)
    return s;
  }

  /// <summary>
  /// Create a resolved expression of the form "e0 - e1".
  /// Optimization: If either "e0" or "e1" is the literal denoting the empty set, then just return "e0".
  /// </summary>
  public static Expression CreateSetDifference(Expression e0, Expression e1) {
    Contract.Requires(e0 != null);
    Contract.Requires(e0.Type != null);
    Contract.Requires(e1 != null);
    Contract.Requires(e1.Type != null);
    Contract.Requires(e0.Type.AsSetType != null && e1.Type.AsSetType != null);
    Contract.Ensures(Contract.Result<Expression>() != null);
    if (LiteralExpr.IsEmptySet(e0) || LiteralExpr.IsEmptySet(e1)) {
      return e0;
    }
    var s = new BinaryExpr(e0.tok, BinaryExpr.Opcode.Sub, e0, e1) {
      ResolvedOp = BinaryExpr.ResolvedOpcode.SetDifference,
      Type = e0.Type.NormalizeExpand() // important to remove any constraints
    };
    return s;
  }

  /// <summary>
  /// Create a resolved expression of the form "e0 - e1".
  /// Optimization: If either "e0" or "e1" is the literal denoting the empty multiset, then just return "e0".
  /// </summary>
  public static Expression CreateMultisetDifference(Expression e0, Expression e1) {
    Contract.Requires(e0 != null);
    Contract.Requires(e0.Type != null);
    Contract.Requires(e1 != null);
    Contract.Requires(e1.Type != null);
    Contract.Requires(e0.Type.AsMultiSetType != null && e1.Type.AsMultiSetType != null);
    Contract.Ensures(Contract.Result<Expression>() != null);
    if (LiteralExpr.IsEmptyMultiset(e0) || LiteralExpr.IsEmptyMultiset(e1)) {
      return e0;
    }
    var s = new BinaryExpr(e0.tok, BinaryExpr.Opcode.Sub, e0, e1) {
      ResolvedOp = BinaryExpr.ResolvedOpcode.MultiSetDifference,
      Type = e0.Type.NormalizeExpand() // important to remove any constraints
    };
    return s;
  }

  /// <summary>
  /// Create a resolved expression of the form "|e|"
  /// </summary>
  public static Expression CreateCardinality(Expression e, BuiltIns builtIns) {
    Contract.Requires(e != null);
    Contract.Requires(e.Type != null);
    Contract.Requires(e.Type.AsSetType != null || e.Type.AsMultiSetType != null || e.Type.AsSeqType != null);
    Contract.Ensures(Contract.Result<Expression>() != null);
    var s = new UnaryOpExpr(e.tok, UnaryOpExpr.Opcode.Cardinality, e) {
      Type = builtIns.Nat()
    };
    return s;
  }

  /// <summary>
  /// Create a resolved expression of the form "e + n"
  /// </summary>
  public static Expression CreateIncrement(Expression e, int n) {
    Contract.Requires(e != null);
    Contract.Requires(e.Type != null);
    Contract.Requires(e.Type.IsNumericBased(Type.NumericPersuasion.Int));
    Contract.Requires(0 <= n);
    Contract.Ensures(Contract.Result<Expression>() != null);
    if (n == 0) {
      return e;
    }
    var nn = CreateIntLiteral(e.tok, n);
    return CreateAdd(e, nn);
  }

  /// <summary>
  /// Create a resolved expression of the form "e - n"
  /// </summary>
  public static Expression CreateDecrement(Expression e, int n) {
    Contract.Requires(e != null);
    Contract.Requires(e.Type.IsNumericBased(Type.NumericPersuasion.Int));
    Contract.Requires(0 <= n);
    Contract.Ensures(Contract.Result<Expression>() != null);
    if (n == 0) {
      return e;
    }
    var nn = CreateIntLiteral(e.tok, n);
    return CreateSubtract(e, nn);
  }

  /// <summary>
  /// Create a resolved expression of the form "n"
  /// </summary>
  public static Expression CreateIntLiteral(IToken tok, int n) {
    Contract.Requires(tok != null);
    Contract.Requires(n != int.MinValue);
    if (0 <= n) {
      var nn = new LiteralExpr(tok, n);
      nn.Type = Type.Int;
      return nn;
    } else {
      return CreateDecrement(CreateIntLiteral(tok, 0), -n);
    }
  }

  /// <summary>
  /// Create a resolved expression of the form "x"
  /// </summary>
  public static Expression CreateRealLiteral(IToken tok, BaseTypes.BigDec x) {
    Contract.Requires(tok != null);
    var nn = new LiteralExpr(tok, x);
    nn.Type = Type.Real;
    return nn;
  }

  /// <summary>
  /// Create a resolved expression of the form "n", for either type "int" or type "ORDINAL".
  /// </summary>
  public static Expression CreateNatLiteral(IToken tok, int n, Type ty) {
    Contract.Requires(tok != null);
    Contract.Requires(0 <= n);
    Contract.Requires(ty.IsNumericBased(Type.NumericPersuasion.Int) || ty is BigOrdinalType);
    var nn = new LiteralExpr(tok, n);
    nn.Type = ty;
    return nn;
  }

  /// <summary>
  /// Create a resolved expression for a bool b
  /// </summary>
  public static LiteralExpr CreateBoolLiteral(IToken tok, bool b) {
    Contract.Requires(tok != null);
    var lit = new LiteralExpr(tok, b);
    lit.Type = Type.Bool;  // resolve here
    return lit;
  }

  /// <summary>
  /// Create a resolved expression for a string s
  /// </summary>
  public static LiteralExpr CreateStringLiteral(IToken tok, string s) {
    Contract.Requires(tok != null);
    Contract.Requires(s != null);
    var lit = new StringLiteralExpr(tok, s, true);
    lit.Type = new SeqType(new CharType());  // resolve here
    return lit;
  }

  /// <summary>
  /// Returns "expr", but with all outer layers of parentheses removed.
  /// This method can be called before resolution.
  /// </summary>
  public static Expression StripParens(Expression expr) {
    while (true) {
      var e = expr as ParensExpression;
      if (e == null) {
        return expr;
      }
      expr = e.E;
    }
  }

  public static ThisExpr AsThis(Expression expr) {
    Contract.Requires(expr != null);
    return StripParens(expr) as ThisExpr;
  }

  /// <summary>
  /// If "expr" denotes a boolean literal "b", then return "true" and set "value" to "b".
  /// Otherwise, return "false" (and the value of "value" should not be used by the caller).
  /// This method can be called before resolution.
  /// </summary>
  public static bool IsBoolLiteral(Expression expr, out bool value) {
    Contract.Requires(expr != null);
    var e = StripParens(expr) as LiteralExpr;
    if (e != null && e.Value is bool) {
      value = (bool)e.Value;
      return true;
    } else {
      value = false;  // to please compiler
      return false;
    }
  }

  /// <summary>
  /// Returns "true" if "expr" denotes the empty set (for "iset", "set", or "multiset").
  /// This method can be called before resolution.
  /// </summary>
  public static bool IsEmptySetOrMultiset(Expression expr) {
    Contract.Requires(expr != null);
    expr = StripParens(expr);
    return (expr is SetDisplayExpr && ((SetDisplayExpr)expr).Elements.Count == 0) ||
           (expr is MultiSetDisplayExpr && ((MultiSetDisplayExpr)expr).Elements.Count == 0);
  }

  public static Expression CreateNot(IToken tok, Expression e) {
    Contract.Requires(tok != null);
    Contract.Requires(e != null && e.Type != null && e.Type.IsBoolType);

    e = StripParens(e);
    if (e is UnaryOpExpr unary && unary.Op == UnaryOpExpr.Opcode.Not) {
      return unary.E;
    }

    if (e is BinaryExpr bin) {
      var negatedOp = BinaryExpr.ResolvedOpcode.Add; // let "Add" stand for "no negated operator"
      switch (bin.ResolvedOp) {
        case BinaryExpr.ResolvedOpcode.EqCommon:
          negatedOp = BinaryExpr.ResolvedOpcode.NeqCommon;
          break;
        case BinaryExpr.ResolvedOpcode.SetEq:
          negatedOp = BinaryExpr.ResolvedOpcode.SetNeq;
          break;
        case BinaryExpr.ResolvedOpcode.MultiSetEq:
          negatedOp = BinaryExpr.ResolvedOpcode.MultiSetNeq;
          break;
        case BinaryExpr.ResolvedOpcode.SeqEq:
          negatedOp = BinaryExpr.ResolvedOpcode.SeqNeq;
          break;
        case BinaryExpr.ResolvedOpcode.MapEq:
          negatedOp = BinaryExpr.ResolvedOpcode.MapNeq;
          break;
        case BinaryExpr.ResolvedOpcode.NeqCommon:
          negatedOp = BinaryExpr.ResolvedOpcode.EqCommon;
          break;
        case BinaryExpr.ResolvedOpcode.SetNeq:
          negatedOp = BinaryExpr.ResolvedOpcode.SetEq;
          break;
        case BinaryExpr.ResolvedOpcode.MultiSetNeq:
          negatedOp = BinaryExpr.ResolvedOpcode.MultiSetEq;
          break;
        case BinaryExpr.ResolvedOpcode.SeqNeq:
          negatedOp = BinaryExpr.ResolvedOpcode.SeqEq;
          break;
        case BinaryExpr.ResolvedOpcode.MapNeq:
          negatedOp = BinaryExpr.ResolvedOpcode.MapEq;
          break;
        default:
          break;
      }
      if (negatedOp != BinaryExpr.ResolvedOpcode.Add) {
        return new BinaryExpr(bin.tok, BinaryExpr.ResolvedOp2SyntacticOp(negatedOp), bin.E0, bin.E1) {
          ResolvedOp = negatedOp,
          Type = bin.Type
        };
      }
    }

    return new UnaryOpExpr(tok, UnaryOpExpr.Opcode.Not, e) {
      Type = Type.Bool
    };
  }

  /// <summary>
  /// Create a resolved expression of the form "e0 LESS e1"
  /// Works for integers, reals, bitvectors, chars, and ORDINALs.
  /// </summary>
  public static Expression CreateLess(Expression e0, Expression e1) {
    Contract.Requires(e0 != null && e0.Type != null);
    Contract.Requires(e1 != null && e1.Type != null);
    Contract.Requires(
      (e0.Type.IsNumericBased(Type.NumericPersuasion.Int) && e1.Type.IsNumericBased(Type.NumericPersuasion.Int)) ||
      (e0.Type.IsNumericBased(Type.NumericPersuasion.Real) && e1.Type.IsNumericBased(Type.NumericPersuasion.Real)) ||
      (e0.Type.IsBitVectorType && e1.Type.IsBitVectorType) ||
      (e0.Type.IsCharType && e1.Type.IsCharType) ||
      (e0.Type.IsBigOrdinalType && e1.Type.IsBigOrdinalType));
    Contract.Ensures(Contract.Result<Expression>() != null);
    return new BinaryExpr(e0.tok, BinaryExpr.Opcode.Lt, e0, e1) {
      ResolvedOp = e0.Type.IsCharType ? BinaryExpr.ResolvedOpcode.LtChar : BinaryExpr.ResolvedOpcode.Lt,
      Type = Type.Bool
    };
  }

  /// <summary>
  /// Create a resolved expression of the form "e0 ATMOST e1".
  /// Works for integers, reals, bitvectors, chars, and ORDINALs.
  /// </summary>
  public static Expression CreateAtMost(Expression e0, Expression e1) {
    Contract.Requires(e0 != null && e0.Type != null);
    Contract.Requires(e1 != null && e1.Type != null);
    Contract.Requires(
      (e0.Type.IsNumericBased(Type.NumericPersuasion.Int) && e1.Type.IsNumericBased(Type.NumericPersuasion.Int)) ||
      (e0.Type.IsNumericBased(Type.NumericPersuasion.Real) && e1.Type.IsNumericBased(Type.NumericPersuasion.Real)) ||
      (e0.Type.IsBitVectorType && e1.Type.IsBitVectorType) ||
      (e0.Type.IsCharType && e1.Type.IsCharType) ||
      (e0.Type.IsBigOrdinalType && e1.Type.IsBigOrdinalType));
    Contract.Ensures(Contract.Result<Expression>() != null);
    return new BinaryExpr(e0.tok, BinaryExpr.Opcode.Le, e0, e1) {
      ResolvedOp = e0.Type.IsCharType ? BinaryExpr.ResolvedOpcode.LeChar : BinaryExpr.ResolvedOpcode.Le,
      Type = Type.Bool
    };
  }

  public static Expression CreateEq(Expression e0, Expression e1, Type ty) {
    Contract.Requires(e0 != null);
    Contract.Requires(e1 != null);
    Contract.Requires(ty != null);
    var eq = new BinaryExpr(e0.tok, BinaryExpr.Opcode.Eq, e0, e1);
    if (ty is SetType) {
      eq.ResolvedOp = BinaryExpr.ResolvedOpcode.SetEq;
    } else if (ty is SeqType) {
      eq.ResolvedOp = BinaryExpr.ResolvedOpcode.SeqEq;
    } else if (ty is MultiSetType) {
      eq.ResolvedOp = BinaryExpr.ResolvedOpcode.MultiSetEq;
    } else if (ty is MapType) {
      eq.ResolvedOp = BinaryExpr.ResolvedOpcode.MapEq;
    } else {
      eq.ResolvedOp = BinaryExpr.ResolvedOpcode.EqCommon;
    }
    eq.type = Type.Bool;
    return eq;
  }

  /// <summary>
  /// Create a resolved expression of the form "e0 && e1"
  /// </summary>
  public static Expression CreateAnd(Expression a, Expression b, bool allowSimplification = true) {
    Contract.Requires(a != null);
    Contract.Requires(b != null);
    Contract.Requires(a.Type.IsBoolType && b.Type.IsBoolType);
    Contract.Ensures(Contract.Result<Expression>() != null);
    if (allowSimplification && LiteralExpr.IsTrue(a)) {
      return b;
    } else if (allowSimplification && LiteralExpr.IsTrue(b)) {
      return a;
    } else {
      var and = new BinaryExpr(a.tok, BinaryExpr.Opcode.And, a, b);
      and.ResolvedOp = BinaryExpr.ResolvedOpcode.And;  // resolve here
      and.Type = Type.Bool;  // resolve here
      return and;
    }
  }

  /// <summary>
  /// Create a resolved expression of the form "e0 ==> e1"
  /// </summary>
  public static Expression CreateImplies(Expression a, Expression b, bool allowSimplification = true) {
    Contract.Requires(a != null);
    Contract.Requires(b != null);
    Contract.Requires(a.Type.IsBoolType && b.Type.IsBoolType);
    Contract.Ensures(Contract.Result<Expression>() != null);
    if (allowSimplification && (LiteralExpr.IsTrue(a) || LiteralExpr.IsTrue(b))) {
      return b;
    } else {
      var imp = new BinaryExpr(a.tok, BinaryExpr.Opcode.Imp, a, b);
      imp.ResolvedOp = BinaryExpr.ResolvedOpcode.Imp;  // resolve here
      imp.Type = Type.Bool;  // resolve here
      return imp;
    }
  }

  /// <summary>
  /// Create a resolved expression of the form "e0 || e1"
  /// </summary>
  public static Expression CreateOr(Expression a, Expression b, bool allowSimplification = true) {
    Contract.Requires(a != null);
    Contract.Requires(b != null);
    Contract.Requires(a.Type.IsBoolType && b.Type.IsBoolType);
    Contract.Ensures(Contract.Result<Expression>() != null);
    if (allowSimplification && LiteralExpr.IsTrue(a)) {
      return a;
    } else if (allowSimplification && LiteralExpr.IsTrue(b)) {
      return b;
    } else {
      var or = new BinaryExpr(a.tok, BinaryExpr.Opcode.Or, a, b);
      or.ResolvedOp = BinaryExpr.ResolvedOpcode.Or;  // resolve here
      or.Type = Type.Bool;  // resolve here
      return or;
    }
  }

  /// <summary>
  /// Create a resolved expression of the form "if test then e0 else e1"
  /// </summary>
  public static Expression CreateITE(Expression test, Expression e0, Expression e1) {
    Contract.Requires(test != null);
    Contract.Requires(e0 != null);
    Contract.Requires(e1 != null);
    Contract.Requires(test.Type.IsBoolType && e0.Type.Equals(e1.Type));
    Contract.Ensures(Contract.Result<Expression>() != null);
    var ite = new ITEExpr(test.tok, false, test, e0, e1);
    ite.Type = e0.type;  // resolve here
    return ite;
  }

  /// <summary>
  /// Create a resolved case expression for a match expression
  /// </summary>
  public static MatchCaseExpr CreateMatchCase(MatchCaseExpr old_case, Expression new_body) {
    Contract.Requires(old_case != null);
    Contract.Requires(new_body != null);
    Contract.Ensures(Contract.Result<MatchCaseExpr>() != null);

    ResolvedCloner cloner = new ResolvedCloner();
    var newVars = old_case.Arguments.ConvertAll(cloner.CloneBoundVar);
    new_body = VarSubstituter(old_case.Arguments.ConvertAll<NonglobalVariable>(x => (NonglobalVariable)x), newVars, new_body);

    var new_case = new MatchCaseExpr(old_case.tok, old_case.Ctor, old_case.FromBoundVar, newVars, new_body, old_case.Attributes);

    new_case.Ctor = old_case.Ctor; // resolve here
    return new_case;
  }

  /// <summary>
  /// Create a match expression with a resolved type
  /// </summary>
  public static Expression CreateMatch(IToken tok, Expression src, List<MatchCaseExpr> cases, Type type) {
    MatchExpr e = new MatchExpr(tok, src, cases, false);
    e.Type = type;  // resolve here

    return e;
  }

  /// <summary>
  /// Create a let expression with a resolved type and fresh variables
  /// </summary>
  public static Expression CreateLet(IToken tok, List<CasePattern<BoundVar>> LHSs, List<Expression> RHSs, Expression body, bool exact) {
    Contract.Requires(tok != null);
    Contract.Requires(LHSs != null && RHSs != null);
    Contract.Requires(LHSs.Count == RHSs.Count);
    Contract.Requires(body != null);

    ResolvedCloner cloner = new ResolvedCloner();
    var newLHSs = LHSs.ConvertAll(cloner.CloneCasePattern);

    var oldVars = new List<BoundVar>();
    LHSs.Iter(p => oldVars.AddRange(p.Vars));
    var newVars = new List<BoundVar>();
    newLHSs.Iter(p => newVars.AddRange(p.Vars));
    body = VarSubstituter(oldVars.ConvertAll<NonglobalVariable>(x => (NonglobalVariable)x), newVars, body);

    var let = new LetExpr(tok, newLHSs, RHSs, body, exact);
    let.Type = body.Type;  // resolve here
    return let;
  }

  /// <summary>
  /// Create a quantifier expression with a resolved type and fresh variables
  /// Optionally replace the old body with the supplied argument
  /// </summary>
  public static Expression CreateQuantifier(QuantifierExpr expr, bool forall, Expression body = null) {
    //(IToken tok, List<BoundVar> vars, Expression range, Expression body, Attributes attribs, Qu) {
    Contract.Requires(expr != null);

    ResolvedCloner cloner = new ResolvedCloner();
    var newVars = expr.BoundVars.ConvertAll(cloner.CloneBoundVar);

    if (body == null) {
      body = expr.Term;
    }

    body = VarSubstituter(expr.BoundVars.ConvertAll<NonglobalVariable>(x => (NonglobalVariable)x), newVars, body);

    QuantifierExpr q;
    if (forall) {
      q = new ForallExpr(expr.tok, expr.BodyEndTok, newVars, expr.Range, body, expr.Attributes);
    } else {
      q = new ExistsExpr(expr.tok, expr.BodyEndTok, newVars, expr.Range, body, expr.Attributes);
    }
    q.Type = Type.Bool;

    return q;
  }

  /// <summary>
  /// Create a resolved IdentifierExpr (whose token is that of the variable)
  /// </summary>
  public static Expression CreateIdentExpr(IVariable v) {
    Contract.Requires(v != null);
    var e = new IdentifierExpr(v.Tok, v.Name);
    e.Var = v;  // resolve here
    e.type = v.Type;  // resolve here
    return e;
  }

  public static Expression VarSubstituter(List<NonglobalVariable> oldVars, List<BoundVar> newVars, Expression e, Dictionary<TypeParameter, Type> typeMap = null) {
    Contract.Requires(oldVars != null && newVars != null);
    Contract.Requires(oldVars.Count == newVars.Count);

    Dictionary<IVariable, Expression/*!*/> substMap = new Dictionary<IVariable, Expression>();
    if (typeMap == null) {
      typeMap = new Dictionary<TypeParameter, Type>();
    }

    for (int i = 0; i < oldVars.Count; i++) {
      var id = new IdentifierExpr(newVars[i].tok, newVars[i].Name);
      id.Var = newVars[i];    // Resolve here manually
      id.Type = newVars[i].Type;  // Resolve here manually
      substMap.Add(oldVars[i], id);
    }

    Substituter sub = new Substituter(null, substMap, typeMap);
    return sub.Substitute(e);
  }

  /// <summary>
  /// Returns the string literal underlying an actual string literal (not as a sequence display of characters)
  /// </summary>
  /// <returns></returns>
  public string AsStringLiteral() {
    var le = this as StringLiteralExpr;
    return le == null ? null : le.Value as string;
  }

  public virtual IEnumerable<INode> Children => SubExpressions;
}

/// <summary>
/// Instances of this class are introduced during resolution to indicate that a static method or function has
/// been invoked without specifying a receiver (that is, by just giving the name of the enclosing class).
/// </summary>
public class StaticReceiverExpr : LiteralExpr {
  public readonly Type UnresolvedType;
  private bool Implicit;
  public Expression OriginalResolved;

  public StaticReceiverExpr(IToken tok, Type t, bool isImplicit)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(t != null);
    UnresolvedType = t;
    Implicit = isImplicit;
    OriginalResolved = null;
  }

  /// <summary>
  /// Constructs a resolved LiteralExpr representing the fictitious static-receiver literal whose type is
  /// "cl" parameterized by the type arguments of "cl" itself.
  /// </summary>
  public StaticReceiverExpr(IToken tok, TopLevelDeclWithMembers cl, bool isImplicit)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(cl != null);
    var typeArgs = cl.TypeArgs.ConvertAll(tp => (Type)new UserDefinedType(tp));
    Type = new UserDefinedType(tok, cl is ClassDecl klass && klass.IsDefaultClass ? cl.Name : cl.Name + "?", cl, typeArgs);
    UnresolvedType = Type;
    Implicit = isImplicit;
  }

  /// <summary>
  /// Constructs a resolved LiteralExpr representing the fictitious literal whose type is
  /// "cl" parameterized according to the type arguments to "t".  It is assumed that "t" denotes
  /// a class or trait that (possibly reflexively or transitively) extends "cl".
  /// Examples:
  /// * If "t" denotes "C(G)" and "cl" denotes "C", then the type of the StaticReceiverExpr
  ///   will be "C(G)".
  /// * Suppose "C" is a class that extends a trait "T"; then, if "t" denotes "C" and "cl" denotes
  ///   "T", then the type of the StaticReceiverExpr will be "T".
  /// * Suppose "C(X)" is a class that extends "T(f(X))", and that "T(Y)" is
  ///   a trait that in turn extends trait "W(g(Y))".  If "t" denotes type "C(G)" and "cl" denotes "W",
  ///   then type of the StaticReceiverExpr will be "T(g(f(G)))".
  /// </summary>
  public StaticReceiverExpr(IToken tok, UserDefinedType t, TopLevelDeclWithMembers cl, bool isImplicit, Expression lhs = null)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(t.ResolvedClass != null);
    Contract.Requires(cl != null);
    var top = t.AsTopLevelTypeWithMembersBypassInternalSynonym;
    if (top != cl) {
      Contract.Assert(top != null);
      var clArgsInTermsOfTFormals = cl.TypeArgs.ConvertAll(tp => top.ParentFormalTypeParametersToActuals[tp]);
      var subst = Resolver.TypeSubstitutionMap(top.TypeArgs, t.TypeArgs);
      var typeArgs = clArgsInTermsOfTFormals.ConvertAll(ty => Resolver.SubstType(ty, subst));
      Type = new UserDefinedType(tok, cl.Name, cl, typeArgs);
    } else if (t.Name != cl.Name) {  // t may be using the name "C?", and we'd prefer it read "C"
      Type = new UserDefinedType(tok, cl.Name, cl, t.TypeArgs);
    } else {
      Type = t;
    }
    UnresolvedType = Type;
    Implicit = isImplicit;
    OriginalResolved = lhs;
  }

  public override bool IsImplicit {
    get { return Implicit; }
  }

  public override IEnumerable<INode> Children => base.Children.Concat(Type.Nodes);
}

public class LiteralExpr : Expression {
  /// <summary>
  /// One of the following:
  ///   * 'null' for the 'null' literal (a special case of which is the subclass StaticReceiverExpr)
  ///   * a bool for a bool literal
  ///   * a BigInteger for int literal
  ///   * a BaseTypes.BigDec for a (rational) real literal
  ///   * a string for a char literal
  ///     This case always uses the subclass CharLiteralExpr.
  ///     Note, a string is stored to keep any escape sequence, since this simplifies printing of the character
  ///     literal, both when pretty printed as a Dafny expression and when being compiled into C# code.  The
  ///     parser checks the validity of any escape sequence and the verifier deals with turning such into a
  ///     single character value.
  ///   * a string for a string literal
  ///     This case always uses the subclass StringLiteralExpr.
  ///     Note, the string is stored with all escapes as characters.  For example, the input string "hello\n" is
  ///     stored in a LiteralExpr has being 7 characters long, whereas the Dafny (and C#) length of this string is 6.
  ///     This simplifies printing of the string, both when pretty printed as a Dafny expression and when being
  ///     compiled into C# code.  The parser checks the validity of the escape sequences and the verifier deals
  ///     with turning them into single characters.
  /// </summary>
  public readonly object Value;

  [Pure]
  public static bool IsTrue(Expression e) {
    Contract.Requires(e != null);
    if (e is LiteralExpr) {
      LiteralExpr le = (LiteralExpr)e;
      return le.Value is bool && (bool)le.Value;
    } else {
      return false;
    }
  }

  public static bool IsEmptySet(Expression e) {
    Contract.Requires(e != null);
    return StripParens(e) is SetDisplayExpr display && display.Elements.Count == 0;
  }

  public static bool IsEmptyMultiset(Expression e) {
    Contract.Requires(e != null);
    return StripParens(e) is MultiSetDisplayExpr display && display.Elements.Count == 0;
  }

  public static bool IsEmptySequence(Expression e) {
    Contract.Requires(e != null);
    return StripParens(e) is SeqDisplayExpr display && display.Elements.Count == 0;
  }

  public LiteralExpr(IToken tok)
    : base(tok) {  // represents the Dafny literal "null"
    Contract.Requires(tok != null);
    this.Value = null;
  }

  public LiteralExpr(IToken tok, BigInteger n)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(0 <= n.Sign);
    this.Value = n;
  }

  public LiteralExpr(IToken tok, BaseTypes.BigDec n)
    : base(tok) {
    Contract.Requires(0 <= n.Mantissa.Sign);
    Contract.Requires(tok != null);
    this.Value = n;
  }

  public LiteralExpr(IToken tok, int n)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(0 <= n);
    this.Value = new BigInteger(n);
  }

  public LiteralExpr(IToken tok, bool b)
    : base(tok) {
    Contract.Requires(tok != null);
    this.Value = b;
  }

  /// <summary>
  /// This constructor is to be used only with the StringLiteralExpr and CharLiteralExpr subclasses, for
  /// two reasons:  both of these literals store a string in .Value, and string literals also carry an
  /// additional field.
  /// </summary>
  protected LiteralExpr(IToken tok, string s)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(s != null);
    this.Value = s;
  }
}

public class CharLiteralExpr : LiteralExpr {
  public CharLiteralExpr(IToken tok, string s)
    : base(tok, s) {
    Contract.Requires(s != null);
  }
}

public class StringLiteralExpr : LiteralExpr {
  public readonly bool IsVerbatim;
  public StringLiteralExpr(IToken tok, string s, bool isVerbatim)
    : base(tok, s) {
    Contract.Requires(s != null);
    IsVerbatim = isVerbatim;
  }
}

public class DatatypeValue : Expression, IHasUsages {
  public readonly string DatatypeName;
  public readonly string MemberName;
  public readonly ActualBindings Bindings;
  public List<Expression> Arguments => Bindings.Arguments;
  [FilledInDuringResolution] public DatatypeCtor Ctor;
  [FilledInDuringResolution] public List<Type> InferredTypeArgs = new List<Type>();
  [FilledInDuringResolution] public bool IsCoCall;
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(DatatypeName != null);
    Contract.Invariant(MemberName != null);
    Contract.Invariant(cce.NonNullElements(Arguments));
    Contract.Invariant(cce.NonNullElements(InferredTypeArgs));
    Contract.Invariant(Ctor == null || InferredTypeArgs.Count == Ctor.EnclosingDatatype.TypeArgs.Count);
  }

  public DatatypeValue(IToken tok, string datatypeName, string memberName, [Captured] List<ActualBinding> arguments)
    : base(tok) {
    Contract.Requires(cce.NonNullElements(arguments));
    Contract.Requires(tok != null);
    Contract.Requires(datatypeName != null);
    Contract.Requires(memberName != null);
    this.DatatypeName = datatypeName;
    this.MemberName = memberName;
    this.Bindings = new ActualBindings(arguments);
  }

  /// <summary>
  /// This constructor is intended to be used when constructing a resolved DatatypeValue. The "args" are expected
  /// to be already resolved, and are all given positionally.
  /// </summary>
  public DatatypeValue(IToken tok, string datatypeName, string memberName, List<Expression> arguments)
    : this(tok, datatypeName, memberName, arguments.ConvertAll(e => new ActualBinding(null, e))) {
    Bindings.AcceptArgumentExpressionsAsExactParameterList();
  }

  public override IEnumerable<Expression> SubExpressions {
    get { return Arguments; }
  }

  public IEnumerable<IDeclarationOrUsage> GetResolvedDeclarations() {
    return Enumerable.Repeat(Ctor, 1);
  }

  public IToken NameToken => tok;
}

public class ThisExpr : Expression {
  public ThisExpr(IToken tok)
    : base(tok) {
    Contract.Requires(tok != null);
  }

  /// <summary>
  /// This constructor creates a ThisExpr and sets its Type field to denote the receiver type
  /// of member "m". This constructor is intended to be used by post-resolution code that needs
  /// to obtain a Dafny "this" expression.
  /// </summary>
  public ThisExpr(MemberDecl m)
    : base(m.tok) {
    Contract.Requires(m != null);
    Contract.Requires(m.tok != null);
    Contract.Requires(m.EnclosingClass != null);
    Contract.Requires(!m.IsStatic);
    Type = Resolver.GetReceiverType(m.tok, m);
  }

  /// <summary>
  /// This constructor creates a ThisExpr and sets its Type field to denote the receiver type
  /// of member "m". This constructor is intended to be used by post-resolution code that needs
  /// to obtain a Dafny "this" expression.
  /// </summary>
  public ThisExpr(TopLevelDeclWithMembers cl)
    : base(cl.tok) {
    Contract.Requires(cl != null);
    Contract.Requires(cl.tok != null);
    Type = Resolver.GetThisType(cl.tok, cl);
  }
}
public class ExpressionPair {
  public Expression A, B;
  public ExpressionPair(Expression a, Expression b) {
    Contract.Requires(a != null);
    Contract.Requires(b != null);
    A = a;
    B = b;
  }
}

public class ImplicitThisExpr : ThisExpr {
  public ImplicitThisExpr(IToken tok)
    : base(tok) {
    Contract.Requires(tok != null);
  }

  public override bool IsImplicit {
    get { return true; }
  }
}

/// <summary>
/// An ImplicitThisExpr_ConstructorCall is used in the .InitCall of a TypeRhs,
/// which has a need for a "throw-away receiver".  Using a different type
/// gives a way to distinguish this receiver from other receivers, which
/// plays a role in checking the restrictions on divided block statements.
/// </summary>
public class ImplicitThisExpr_ConstructorCall : ImplicitThisExpr {
  public ImplicitThisExpr_ConstructorCall(IToken tok)
    : base(tok) {
    Contract.Requires(tok != null);
  }
}

public class IdentifierExpr : Expression, IHasUsages {
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(Name != null);
  }

  public readonly string Name;
  [FilledInDuringResolution] public IVariable Var;

  public IdentifierExpr(IToken tok, string name)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(name != null);
    Name = name;
  }
  /// <summary>
  /// Constructs a resolved IdentifierExpr.
  /// </summary>
  public IdentifierExpr(IToken tok, IVariable v)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(v != null);
    Name = v.Name;
    Var = v;
    Type = v.Type;
  }

  public IEnumerable<IDeclarationOrUsage> GetResolvedDeclarations() {
    return Enumerable.Repeat(Var, 1);
  }

  public IToken NameToken => tok;
}

/// <summary>
/// If an "AutoGhostIdentifierExpr" is used as the out-parameter of a ghost method or
/// a method with a ghost parameter, resolution will change the .Var's .IsGhost to true
/// automatically.  This class is intended to be used only as a communicate between the
/// parser and parts of the resolver.
/// </summary>
public class AutoGhostIdentifierExpr : IdentifierExpr {
  public AutoGhostIdentifierExpr(IToken tok, string name)
    : base(new AutoGeneratedToken(tok), name) { }
}

/// <summary>
/// This class is used only inside the resolver itself. It gets hung in the AST in uncompleted name segments.
/// </summary>
class Resolver_IdentifierExpr : Expression, IHasUsages {
  public readonly TopLevelDecl Decl;
  public readonly List<Type> TypeArgs;
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(Decl != null);
    Contract.Invariant(TypeArgs != null);
    Contract.Invariant(TypeArgs.Count == Decl.TypeArgs.Count);
    Contract.Invariant(Type is ResolverType_Module || Type is ResolverType_Type);
  }

  public override IEnumerable<INode> Children => TypeArgs.SelectMany(ta => ta.Nodes);

  public abstract class ResolverType : Type {
    public override bool ComputeMayInvolveReferences(ISet<DatatypeDecl>/*?*/ visitedDatatypes) {
      return false;
    }
  }
  public class ResolverType_Module : ResolverType {
    [Pure]
    public override string TypeName(ModuleDefinition context, bool parseAble) {
      Contract.Assert(parseAble == false);
      return "#module";
    }
    public override bool Equals(Type that, bool keepConstraints = false) {
      return that.NormalizeExpand(keepConstraints) is ResolverType_Module;
    }
  }
  public class ResolverType_Type : ResolverType {
    [Pure]
    public override string TypeName(ModuleDefinition context, bool parseAble) {
      Contract.Assert(parseAble == false);
      return "#type";
    }
    public override bool Equals(Type that, bool keepConstraints = false) {
      return that.NormalizeExpand(keepConstraints) is ResolverType_Type;
    }
  }

  public Resolver_IdentifierExpr(IToken tok, TopLevelDecl decl, List<Type> typeArgs)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(decl != null);
    Contract.Requires(typeArgs != null && typeArgs.Count == decl.TypeArgs.Count);
    Decl = decl;
    TypeArgs = typeArgs;
    Type = decl is ModuleDecl ? (Type)new ResolverType_Module() : new ResolverType_Type();
  }
  public Resolver_IdentifierExpr(IToken tok, TypeParameter tp)
    : this(tok, tp, new List<Type>()) {
    Contract.Requires(tok != null);
    Contract.Requires(tp != null);
  }

  public IEnumerable<IDeclarationOrUsage> GetResolvedDeclarations() {
    return new[] { Decl };
  }

  public IToken NameToken => tok;
}

public abstract class DisplayExpression : Expression {
  public readonly List<Expression> Elements;
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(cce.NonNullElements(Elements));
  }

  public DisplayExpression(IToken tok, List<Expression> elements)
    : base(tok) {
    Contract.Requires(cce.NonNullElements(elements));
    Elements = elements;
  }

  public override IEnumerable<Expression> SubExpressions {
    get { return Elements; }
  }
}

public class SetDisplayExpr : DisplayExpression {
  public bool Finite;
  public SetDisplayExpr(IToken tok, bool finite, List<Expression> elements)
    : base(tok, elements) {
    Contract.Requires(tok != null);
    Contract.Requires(cce.NonNullElements(elements));
    Finite = finite;
  }
}

public class MultiSetDisplayExpr : DisplayExpression {
  public MultiSetDisplayExpr(IToken tok, List<Expression> elements) : base(tok, elements) {
    Contract.Requires(tok != null);
    Contract.Requires(cce.NonNullElements(elements));
  }
}

public class MapDisplayExpr : Expression {
  public bool Finite;
  public List<ExpressionPair> Elements;
  public MapDisplayExpr(IToken tok, bool finite, List<ExpressionPair> elements)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(cce.NonNullElements(elements));
    Finite = finite;
    Elements = elements;
  }
  public override IEnumerable<Expression> SubExpressions {
    get {
      foreach (var ep in Elements) {
        yield return ep.A;
        yield return ep.B;
      }
    }
  }
}
public class SeqDisplayExpr : DisplayExpression {
  public SeqDisplayExpr(IToken tok, List<Expression> elements)
    : base(tok, elements) {
    Contract.Requires(cce.NonNullElements(elements));
    Contract.Requires(tok != null);
  }
}

public class MemberSelectExpr : Expression, IHasUsages {
  public readonly Expression Obj;
  public string MemberName;
  [FilledInDuringResolution] public MemberDecl Member;    // will be a Field or Function
  [FilledInDuringResolution] public Label /*?*/ AtLabel;  // non-null for a two-state selection
  [FilledInDuringResolution] public bool InCompiledContext;

  /// <summary>
  /// TypeApplication_AtEnclosingClass is the list of type arguments used to instantiate the type that
  /// declares Member (which is some supertype of the receiver type).
  /// </summary>
  [FilledInDuringResolution] public List<Type> TypeApplication_AtEnclosingClass;

  /// <summary>
  ///  TypeApplication_JustMember is the list of type arguments used to instantiate the type parameters
  /// of Member.
  /// </summary>
  [FilledInDuringResolution] public List<Type> TypeApplication_JustMember;

  /// <summary>
  /// Returns a mapping from formal type parameters to actual type arguments. For example, given
  ///     trait T<A> {
  ///       function F<X>(): bv8 { ... }
  ///     }
  ///     class C<B, D> extends T<map<B, D>> { }
  /// and MemberSelectExpr o.F<int> where o has type C<real, bool>, the type map returned is
  ///     A -> map<real, bool>
  ///     X -> int
  /// To also include B and D in the mapping, use TypeArgumentSubstitutionsWithParents instead.
  /// </summary>
  public Dictionary<TypeParameter, Type> TypeArgumentSubstitutionsAtMemberDeclaration() {
    Contract.Requires(WasResolved());
    Contract.Ensures(Contract.Result<Dictionary<TypeParameter, Type>>() != null);

    var subst = new Dictionary<TypeParameter, Type>();

    // Add the mappings from the member's own type parameters
    if (Member is ICallable icallable) {
      Contract.Assert(TypeApplication_JustMember.Count == icallable.TypeArgs.Count);
      for (var i = 0; i < icallable.TypeArgs.Count; i++) {
        subst.Add(icallable.TypeArgs[i], TypeApplication_JustMember[i]);
      }
    } else {
      Contract.Assert(TypeApplication_JustMember.Count == 0);
    }

    // Add the mappings from the enclosing class.
    TopLevelDecl cl = Member.EnclosingClass;
    // Expand the type down to its non-null type, if any
    if (cl != null) {
      Contract.Assert(cl.TypeArgs.Count == TypeApplication_AtEnclosingClass.Count);
      for (var i = 0; i < cl.TypeArgs.Count; i++) {
        subst.Add(cl.TypeArgs[i], TypeApplication_AtEnclosingClass[i]);
      }
    }

    return subst;
  }

  /// <summary>
  /// Returns a mapping from formal type parameters to actual type arguments. For example, given
  ///     trait T<A> {
  ///       function F<X>(): bv8 { ... }
  ///     }
  ///     class C<B, D> extends T<map<B, D>> { }
  /// and MemberSelectExpr o.F<int> where o has type C<real, bool>, the type map returned is
  ///     A -> map<real, bool>
  ///     B -> real
  ///     D -> bool
  ///     X -> int
  /// NOTE: This method should be called only when all types have been fully and successfully
  /// resolved. During type inference, when there may still be some unresolved proxies, use
  /// TypeArgumentSubstitutionsAtMemberDeclaration instead.
  /// </summary>
  public Dictionary<TypeParameter, Type> TypeArgumentSubstitutionsWithParents() {
    Contract.Requires(WasResolved());
    Contract.Ensures(Contract.Result<Dictionary<TypeParameter, Type>>() != null);

    return TypeArgumentSubstitutionsWithParentsAux(Obj.Type, Member, TypeApplication_JustMember);
  }

  public static Dictionary<TypeParameter, Type> TypeArgumentSubstitutionsWithParentsAux(Type receiverType, MemberDecl member, List<Type> typeApplicationMember) {
    Contract.Requires(receiverType != null);
    Contract.Requires(member != null);
    Contract.Requires(typeApplicationMember != null);
    Contract.Ensures(Contract.Result<Dictionary<TypeParameter, Type>>() != null);

    var subst = new Dictionary<TypeParameter, Type>();

    // Add the mappings from the member's own type parameters
    if (member is ICallable) {
      // Make sure to include the member's type parameters all the way up the inheritance chain
      for (var ancestor = member; ancestor != null; ancestor = ancestor.OverriddenMember) {
        var icallable = (ICallable)ancestor;
        Contract.Assert(typeApplicationMember.Count == icallable.TypeArgs.Count);
        for (var i = 0; i < icallable.TypeArgs.Count; i++) {
          subst.Add(icallable.TypeArgs[i], typeApplicationMember[i]);
        }
      }
    } else {
      Contract.Assert(typeApplicationMember.Count == 0);
    }

    // Add the mappings from the receiver's type "cl"
    var udt = receiverType.NormalizeExpand() as UserDefinedType;
    if (udt != null) {
      if (udt.ResolvedClass is InternalTypeSynonymDecl isyn) {
        udt = isyn.RhsWithArgumentIgnoringScope(udt.TypeArgs) as UserDefinedType;
      }
      if (udt.ResolvedClass is NonNullTypeDecl nntd) {
        udt = nntd.RhsWithArgumentIgnoringScope(udt.TypeArgs) as UserDefinedType;
      }
    }
    var cl = udt?.ResolvedClass;

    if (cl != null) {
      Contract.Assert(cl.TypeArgs.Count == udt.TypeArgs.Count);
      for (var i = 0; i < cl.TypeArgs.Count; i++) {
        subst.Add(cl.TypeArgs[i], udt.TypeArgs[i]);
      }

      // Add in the mappings from parent types' formal type parameters to types
      if (cl is TopLevelDeclWithMembers cls) {
        foreach (var entry in cls.ParentFormalTypeParametersToActuals) {
          var v = Resolver.SubstType(entry.Value, subst);
          subst.Add(entry.Key, v);
        }
      }
    }

    return subst;
  }

  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(Obj != null);
    Contract.Invariant(MemberName != null);
    Contract.Invariant((Member != null) == (TypeApplication_AtEnclosingClass != null));  // TypeApplication_* are set whenever Member is set
    Contract.Invariant((Member != null) == (TypeApplication_JustMember != null));  // TypeApplication_* are set whenever Member is set
  }

  public MemberSelectExpr(IToken tok, Expression obj, string memberName)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(obj != null);
    Contract.Requires(memberName != null);
    this.Obj = obj;
    this.MemberName = memberName;
  }

  /// <summary>
  /// Returns a resolved MemberSelectExpr for a field.
  /// </summary>
  public MemberSelectExpr(IToken tok, Expression obj, Field field)
    : this(tok, obj, field.Name) {
    Contract.Requires(tok != null);
    Contract.Requires(obj != null);
    Contract.Requires(field != null);
    Contract.Requires(obj.Type != null);  // "obj" is required to be resolved

    this.Member = field;  // resolve here

    var receiverType = obj.Type.NormalizeExpand();
    this.TypeApplication_AtEnclosingClass = receiverType.TypeArgs;
    this.TypeApplication_JustMember = new List<Type>();
    this.ResolvedOutparameterTypes = new List<Type>();

    var typeMap = new Dictionary<TypeParameter, Type>();
    if (receiverType is UserDefinedType udt) {
      var cl = udt.ResolvedClass as TopLevelDeclWithMembers;
      Contract.Assert(cl != null);
      Contract.Assert(cl.TypeArgs.Count == TypeApplication_AtEnclosingClass.Count);
      for (var i = 0; i < cl.TypeArgs.Count; i++) {
        typeMap.Add(cl.TypeArgs[i], TypeApplication_AtEnclosingClass[i]);
      }
      foreach (var entry in cl.ParentFormalTypeParametersToActuals) {
        var v = Resolver.SubstType(entry.Value, typeMap);
        typeMap.Add(entry.Key, v);
      }
    } else if (field.EnclosingClass == null) {
      // leave typeMap as the empty substitution
    } else {
      Contract.Assert(field.EnclosingClass.TypeArgs.Count == TypeApplication_AtEnclosingClass.Count);
      for (var i = 0; i < field.EnclosingClass.TypeArgs.Count; i++) {
        typeMap.Add(field.EnclosingClass.TypeArgs[i], TypeApplication_AtEnclosingClass[i]);
      }
    }
    this.Type = Resolver.SubstType(field.Type, typeMap);  // resolve here
  }

  public void MemberSelectCase(Action<Field> fieldK, Action<Function> functionK) {
    MemberSelectCase<bool>(
      f => {
        fieldK(f);
        return true;
      },
      f => {
        functionK(f);
        return true;
      });
  }

  public A MemberSelectCase<A>(Func<Field, A> fieldK, Func<Function, A> functionK) {
    var field = Member as Field;
    var function = Member as Function;
    if (field != null) {
      return fieldK(field);
    } else {
      Contract.Assert(function != null);
      return functionK(function);
    }
  }

  public override IEnumerable<Expression> SubExpressions {
    get { yield return Obj; }
  }

  public override IEnumerable<Type> ComponentTypes => Util.Concat(TypeApplication_AtEnclosingClass, TypeApplication_JustMember);

  [FilledInDuringResolution] public List<Type> ResolvedOutparameterTypes;

  public IEnumerable<IDeclarationOrUsage> GetResolvedDeclarations() {
    return new[] { Member };
  }

  public IToken NameToken => tok;
}

public class SeqSelectExpr : Expression {
  public readonly bool SelectOne;  // false means select a range
  public readonly Expression Seq;
  public readonly Expression E0;
  public readonly Expression E1;
  public readonly IToken CloseParen;

  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(Seq != null);
    Contract.Invariant(!SelectOne || E1 == null);
  }

  public SeqSelectExpr(IToken tok, bool selectOne, Expression seq, Expression e0, Expression e1, IToken closeParen)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(seq != null);
    Contract.Requires(!selectOne || e1 == null);

    SelectOne = selectOne;
    Seq = seq;
    E0 = e0;
    E1 = e1;
    CloseParen = closeParen;
    if (closeParen != null) {
      FormatTokens = new[] { closeParen };
    }
  }

  public override IEnumerable<Expression> SubExpressions {
    get {
      yield return Seq;
      if (E0 != null) {
        yield return E0;
      }

      if (E1 != null) {
        yield return E1;
      }
    }
  }
}

public class MultiSelectExpr : Expression {
  public readonly Expression Array;
  public readonly List<Expression> Indices;
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(Array != null);
    Contract.Invariant(cce.NonNullElements(Indices));
    Contract.Invariant(1 <= Indices.Count);
  }

  public MultiSelectExpr(IToken tok, Expression array, List<Expression> indices)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(array != null);
    Contract.Requires(cce.NonNullElements(indices) && 1 <= indices.Count);

    Array = array;
    Indices = indices;
  }

  public override IEnumerable<Expression> SubExpressions {
    get {
      yield return Array;
      foreach (var e in Indices) {
        yield return e;
      }
    }
  }
}

/// <summary>
/// Represents an expression of the form S[I := V], where, syntactically, S, I, and V are expressions.
///
/// Successfully resolved, the expression stands for one of the following:
/// * if S is a seq<T>, then I is an integer-based index into the sequence and V is of type T
/// * if S is a map<T, U>, then I is a key of type T and V is a value of type U
/// * if S is a multiset<T>, then I is an element of type T and V has an integer-based numeric type.
///
/// Datatype updates are represented by <c>DatatypeUpdateExpr</c> nodes.
/// </summary>
public class SeqUpdateExpr : Expression {
  public readonly Expression Seq;
  public readonly Expression Index;
  public readonly Expression Value;
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(Seq != null);
    Contract.Invariant(Index != null);
    Contract.Invariant(Value != null);
  }

  public SeqUpdateExpr(IToken tok, Expression seq, Expression index, Expression val)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(seq != null);
    Contract.Requires(index != null);
    Contract.Requires(val != null);
    Seq = seq;
    Index = index;
    Value = val;
  }

  public override IEnumerable<Expression> SubExpressions {
    get {
      yield return Seq;
      yield return Index;
      yield return Value;
    }
  }
}

public class ApplyExpr : Expression {
  // The idea is that this apply expression does not need a type argument substitution,
  // since lambda functions and anonymous functions are never polymorphic.
  // Make a FunctionCallExpr otherwise, to call a resolvable anonymous function.
  public readonly Expression Function;
  public readonly List<Expression> Args;

  public override IEnumerable<Expression> SubExpressions {
    get {
      yield return Function;
      foreach (var e in Args) {
        yield return e;
      }
    }
  }

  public IToken CloseParen;

  public ApplyExpr(IToken tok, Expression fn, List<Expression> args, IToken closeParen)
    : base(tok) {
    Function = fn;
    Args = args;
    CloseParen = closeParen;
    FormatTokens = closeParen != null ? new[] { closeParen } : null;
  }
}

public class FunctionCallExpr : Expression, IHasUsages {
  public string Name;
  public readonly Expression Receiver;
  public readonly IToken OpenParen;  // can be null if Args.Count == 0
  public readonly IToken CloseParen;
  public readonly Label/*?*/ AtLabel;
  public readonly ActualBindings Bindings;
  public List<Expression> Args => Bindings.Arguments;
  [FilledInDuringResolution] public List<Type> TypeApplication_AtEnclosingClass;
  [FilledInDuringResolution] public List<Type> TypeApplication_JustFunction;
  [FilledInDuringResolution] public bool IsByMethodCall;

  /// <summary>
  /// Return a mapping from each type parameter of the function and its enclosing class to actual type arguments.
  /// This method should only be called on fully and successfully resolved FunctionCallExpr's.
  /// </summary>
  public Dictionary<TypeParameter, Type> GetTypeArgumentSubstitutions() {
    var typeMap = new Dictionary<TypeParameter, Type>();
    Util.AddToDict(typeMap, Function.EnclosingClass.TypeArgs, TypeApplication_AtEnclosingClass);
    Util.AddToDict(typeMap, Function.TypeArgs, TypeApplication_JustFunction);
    return typeMap;
  }

  /// <summary>
  /// Returns a mapping from formal type parameters to actual type arguments. For example, given
  ///     trait T<A> {
  ///       function F<X>(): bv8 { ... }
  ///     }
  ///     class C<B, D> extends T<map<B, D>> { }
  /// and FunctionCallExpr o.F<int>(args) where o has type C<real, bool>, the type map returned is
  ///     A -> map<real, bool>
  ///     B -> real
  ///     D -> bool
  ///     X -> int
  /// NOTE: This method should be called only when all types have been fully and successfully
  /// resolved.
  /// </summary>
  public Dictionary<TypeParameter, Type> TypeArgumentSubstitutionsWithParents() {
    Contract.Requires(WasResolved());
    Contract.Ensures(Contract.Result<Dictionary<TypeParameter, Type>>() != null);

    return MemberSelectExpr.TypeArgumentSubstitutionsWithParentsAux(Receiver.Type, Function, TypeApplication_JustFunction);
  }

  public enum CoCallResolution {
    No,
    Yes,
    NoBecauseFunctionHasSideEffects,
    NoBecauseFunctionHasPostcondition,
    NoBecauseRecursiveCallsAreNotAllowedInThisContext,
    NoBecauseIsNotGuarded,
    NoBecauseRecursiveCallsInDestructiveContext
  }
  [FilledInDuringResolution] public CoCallResolution CoCall = CoCallResolution.No;  // indicates whether or not the call is a co-recursive call
  [FilledInDuringResolution] public string CoCallHint = null;  // possible additional hint that can be used in verifier error message

  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(Name != null);
    Contract.Invariant(Receiver != null);
    Contract.Invariant(cce.NonNullElements(Args));
    Contract.Invariant(
      Function == null || TypeApplication_AtEnclosingClass == null ||
      Function.EnclosingClass.TypeArgs.Count == TypeApplication_AtEnclosingClass.Count);
    Contract.Invariant(
      Function == null || TypeApplication_JustFunction == null ||
      Function.TypeArgs.Count == TypeApplication_JustFunction.Count);
  }

  [FilledInDuringResolution] public Function Function;

  public FunctionCallExpr(IToken tok, string fn, Expression receiver, IToken openParen, IToken closeParen, [Captured] List<ActualBinding> args, Label/*?*/ atLabel = null)
    : this(tok, fn, receiver, openParen, closeParen, new ActualBindings(args), atLabel) {
    Contract.Requires(tok != null);
    Contract.Requires(fn != null);
    Contract.Requires(receiver != null);
    Contract.Requires(cce.NonNullElements(args));
    Contract.Requires(openParen != null || args.Count == 0);
    Contract.Ensures(type == null);
  }

  public FunctionCallExpr(IToken tok, string fn, Expression receiver, IToken openParen, IToken closeParen, [Captured] ActualBindings bindings, Label/*?*/ atLabel = null)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(fn != null);
    Contract.Requires(receiver != null);
    Contract.Requires(bindings != null);
    Contract.Requires(openParen != null);
    Contract.Ensures(type == null);

    this.Name = fn;
    this.Receiver = receiver;
    this.OpenParen = openParen;
    this.CloseParen = closeParen;
    this.AtLabel = atLabel;
    this.Bindings = bindings;
    this.FormatTokens = closeParen != null ? new[] { closeParen } : null;
  }

  /// <summary>
  /// This constructor is intended to be used when constructing a resolved FunctionCallExpr. The "args" are expected
  /// to be already resolved, and are all given positionally.
  /// </summary>
  public FunctionCallExpr(IToken tok, string fn, Expression receiver, IToken openParen, IToken closeParen, [Captured] List<Expression> args,
    Label /*?*/ atLabel = null)
    : this(tok, fn, receiver, openParen, closeParen, args.ConvertAll(e => new ActualBinding(null, e)), atLabel) {
    Bindings.AcceptArgumentExpressionsAsExactParameterList();
  }

  public override IEnumerable<Expression> SubExpressions {
    get {
      yield return Receiver;
      foreach (var e in Args) {
        yield return e;
      }
    }
  }

  public override IEnumerable<Type> ComponentTypes => Util.Concat(TypeApplication_AtEnclosingClass, TypeApplication_JustFunction);
  public IEnumerable<IDeclarationOrUsage> GetResolvedDeclarations() {
    return Enumerable.Repeat(Function, 1);
  }

  public IToken NameToken => tok;
}

public class SeqConstructionExpr : Expression {
  public Type/*?*/ ExplicitElementType;
  public Expression N;
  public Expression Initializer;
  public SeqConstructionExpr(IToken tok, Type/*?*/ elementType, Expression length, Expression initializer)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(length != null);
    Contract.Requires(initializer != null);
    ExplicitElementType = elementType;
    N = length;
    Initializer = initializer;
  }
  public override IEnumerable<Expression> SubExpressions {
    get {
      yield return N;
      yield return Initializer;
    }
  }

  public override IEnumerable<Type> ComponentTypes {
    get {
      if (ExplicitElementType != null) {
        yield return ExplicitElementType;
      }
    }
  }
}

public class MultiSetFormingExpr : Expression {
  [Peer]
  public readonly Expression E;
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(E != null);
  }

  [Captured]
  public MultiSetFormingExpr(IToken tok, Expression expr)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(expr != null);
    cce.Owner.AssignSame(this, expr);
    E = expr;
  }

  public override IEnumerable<Expression> SubExpressions {
    get { yield return E; }
  }
}

public class OldExpr : Expression {
  [Peer]
  public readonly Expression E;
  public readonly string/*?*/ At;
  [FilledInDuringResolution] public Label/*?*/ AtLabel;  // after that, At==null iff AtLabel==null
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(E != null);
  }

  [Captured]
  public OldExpr(IToken tok, Expression expr, string at = null)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(expr != null);
    cce.Owner.AssignSame(this, expr);
    E = expr;
    At = at;
  }

  public override IEnumerable<Expression> SubExpressions {
    get { yield return E; }
  }
}

public class UnchangedExpr : Expression {
  public readonly List<FrameExpression> Frame;
  public readonly string/*?*/ At;
  [FilledInDuringResolution] public Label/*?*/ AtLabel;  // after that, At==null iff AtLabel==null
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(Frame != null);
  }

  public UnchangedExpr(IToken tok, List<FrameExpression> frame, string/*?*/ at)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(frame != null);
    this.Frame = frame;
    this.At = at;
  }

  public override IEnumerable<Expression> SubExpressions {
    get {
      foreach (var fe in Frame) {
        yield return fe.E;
      }
    }
  }
}

public abstract class UnaryExpr : Expression {
  public readonly Expression E;
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(E != null);
  }

  public UnaryExpr(IToken tok, Expression e)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(e != null);
    this.E = e;
  }

  public override IEnumerable<Expression> SubExpressions {
    get { yield return E; }
  }
}

public class UnaryOpExpr : UnaryExpr {
  public enum Opcode {
    Not,  // boolean negation or bitwise negation
    Cardinality,
    Fresh, // fresh also has a(n optional) second argument, namely the @-label
    Allocated,
    Lit,  // there is no syntax for this operator, but it is sometimes introduced during translation
  }
  public readonly Opcode Op;

  public enum ResolvedOpcode {
    YetUndetermined,
    BVNot,
    BoolNot,
    SeqLength,
    SetCard,
    MultiSetCard,
    MapCard,
    Fresh,
    Allocated,
    Lit
  }

  private ResolvedOpcode _ResolvedOp = ResolvedOpcode.YetUndetermined;
  public ResolvedOpcode ResolvedOp => ResolveOp();

  public ResolvedOpcode ResolveOp() {
    if (_ResolvedOp == ResolvedOpcode.YetUndetermined) {
      Contract.Assert(Type != null);
      Contract.Assert(Type is not TypeProxy);
      _ResolvedOp = (Op, E.Type.NormalizeExpand()) switch {
        (Opcode.Not, BoolType _) => ResolvedOpcode.BoolNot,
        (Opcode.Not, BitvectorType _) => ResolvedOpcode.BVNot,
        (Opcode.Cardinality, SeqType _) => ResolvedOpcode.SeqLength,
        (Opcode.Cardinality, SetType _) => ResolvedOpcode.SetCard,
        (Opcode.Cardinality, MultiSetType _) => ResolvedOpcode.MultiSetCard,
        (Opcode.Cardinality, MapType _) => ResolvedOpcode.MapCard,
        (Opcode.Fresh, _) => ResolvedOpcode.Fresh,
        (Opcode.Allocated, _) => ResolvedOpcode.Allocated,
        (Opcode.Lit, _) => ResolvedOpcode.Lit,
        _ => ResolvedOpcode.YetUndetermined // Unreachable
      };
      Contract.Assert(_ResolvedOp != ResolvedOpcode.YetUndetermined);
    }

    return _ResolvedOp;
  }

  public UnaryOpExpr(IToken tok, Opcode op, Expression e)
    : base(tok, e) {
    Contract.Requires(tok != null);
    Contract.Requires(e != null);
    Contract.Requires(op != Opcode.Fresh || this is FreshExpr);
    this.Op = op;
  }
}

public class FreshExpr : UnaryOpExpr {
  public readonly string/*?*/ At;
  [FilledInDuringResolution] public Label/*?*/ AtLabel;  // after that, At==null iff AtLabel==null

  public FreshExpr(IToken tok, Expression e, string at = null)
    : base(tok, Opcode.Fresh, e) {
    Contract.Requires(tok != null);
    Contract.Requires(e != null);
    this.At = at;
  }
}

public abstract class TypeUnaryExpr : UnaryExpr {
  public readonly Type ToType;
  public TypeUnaryExpr(IToken tok, Expression expr, Type toType)
    : base(tok, expr) {
    Contract.Requires(tok != null);
    Contract.Requires(expr != null);
    Contract.Requires(toType != null);
    ToType = toType;
  }

  public override IEnumerable<INode> Children => base.Children.Concat(ToType.Nodes);

  public override IEnumerable<Type> ComponentTypes {
    get {
      yield return ToType;
    }
  }
}

public class ConversionExpr : TypeUnaryExpr {
  public readonly string messagePrefix;
  public ConversionExpr(IToken tok, Expression expr, Type toType, string messagePrefix = "")
    : base(tok, expr, toType) {
    Contract.Requires(tok != null);
    Contract.Requires(expr != null);
    Contract.Requires(toType != null);
    this.messagePrefix = messagePrefix;
  }
}

public class TypeTestExpr : TypeUnaryExpr {
  public TypeTestExpr(IToken tok, Expression expr, Type toType)
    : base(tok, expr, toType) {
    Contract.Requires(tok != null);
    Contract.Requires(expr != null);
    Contract.Requires(toType != null);
  }
}

public class BinaryExpr : Expression {
  public enum Opcode {
    Iff,
    Imp,
    Exp, // turned into Imp during resolution
    And,
    Or,
    Eq,
    Neq,
    Lt,
    Le,
    Ge,
    Gt,
    Disjoint,
    In,
    NotIn,
    LeftShift,
    RightShift,
    Add,
    Sub,
    Mul,
    Div,
    Mod,
    BitwiseAnd,
    BitwiseOr,
    BitwiseXor
  }
  public readonly Opcode Op;
  public enum ResolvedOpcode {
    YetUndetermined,  // the value before resolution has determined the value; .ResolvedOp should never be read in this state

    // logical operators
    Iff,
    Imp,
    And,
    Or,
    // non-collection types
    EqCommon,
    NeqCommon,
    // integers, reals, bitvectors
    Lt,
    LessThanLimit,  // a synonym for Lt for ORDINAL, used only during translation
    Le,
    Ge,
    Gt,
    Add,
    Sub,
    Mul,
    Div,
    Mod,
    // bitvectors
    LeftShift,
    RightShift,
    BitwiseAnd,
    BitwiseOr,
    BitwiseXor,
    // char
    LtChar,
    LeChar,
    GeChar,
    GtChar,
    // sets
    SetEq,
    SetNeq,
    ProperSubset,
    Subset,
    Superset,
    ProperSuperset,
    Disjoint,
    InSet,
    NotInSet,
    Union,
    Intersection,
    SetDifference,
    // multi-sets
    MultiSetEq,
    MultiSetNeq,
    MultiSubset,
    MultiSuperset,
    ProperMultiSubset,
    ProperMultiSuperset,
    MultiSetDisjoint,
    InMultiSet,
    NotInMultiSet,
    MultiSetUnion,
    MultiSetIntersection,
    MultiSetDifference,
    // Sequences
    SeqEq,
    SeqNeq,
    ProperPrefix,
    Prefix,
    Concat,
    InSeq,
    NotInSeq,
    // Maps
    MapEq,
    MapNeq,
    InMap,
    NotInMap,
    MapMerge,
    MapSubtraction,
    // datatypes
    RankLt,
    RankGt
  }
  private ResolvedOpcode _theResolvedOp = ResolvedOpcode.YetUndetermined;
  public ResolvedOpcode ResolvedOp {
    set {
      Contract.Assume(_theResolvedOp == ResolvedOpcode.YetUndetermined || _theResolvedOp == value);  // there's never a reason for resolution to change its mind, is there?
      _theResolvedOp = value;
    }
    get {
      Contract.Assume(_theResolvedOp != ResolvedOpcode.YetUndetermined);  // shouldn't read it until it has been properly initialized
      return _theResolvedOp;
    }
  }
  public ResolvedOpcode ResolvedOp_PossiblyStillUndetermined {  // offer a way to return _theResolveOp -- for experts only!
    get { return _theResolvedOp; }
  }
  public static bool IsEqualityOp(ResolvedOpcode op) {
    switch (op) {
      case ResolvedOpcode.EqCommon:
      case ResolvedOpcode.SetEq:
      case ResolvedOpcode.SeqEq:
      case ResolvedOpcode.MultiSetEq:
      case ResolvedOpcode.MapEq:
        return true;
      default:
        return false;
    }
  }

  public static Opcode ResolvedOp2SyntacticOp(ResolvedOpcode rop) {
    switch (rop) {
      case ResolvedOpcode.Iff: return Opcode.Iff;
      case ResolvedOpcode.Imp: return Opcode.Imp;
      case ResolvedOpcode.And: return Opcode.And;
      case ResolvedOpcode.Or: return Opcode.Or;

      case ResolvedOpcode.EqCommon:
      case ResolvedOpcode.SetEq:
      case ResolvedOpcode.MultiSetEq:
      case ResolvedOpcode.SeqEq:
      case ResolvedOpcode.MapEq:
        return Opcode.Eq;

      case ResolvedOpcode.NeqCommon:
      case ResolvedOpcode.SetNeq:
      case ResolvedOpcode.MultiSetNeq:
      case ResolvedOpcode.SeqNeq:
      case ResolvedOpcode.MapNeq:
        return Opcode.Neq;

      case ResolvedOpcode.Lt:
      case ResolvedOpcode.LtChar:
      case ResolvedOpcode.ProperSubset:
      case ResolvedOpcode.ProperMultiSuperset:
      case ResolvedOpcode.ProperPrefix:
      case ResolvedOpcode.RankLt:
        return Opcode.Lt;

      case ResolvedOpcode.Le:
      case ResolvedOpcode.LeChar:
      case ResolvedOpcode.Subset:
      case ResolvedOpcode.MultiSubset:
      case ResolvedOpcode.Prefix:
        return Opcode.Le;

      case ResolvedOpcode.Ge:
      case ResolvedOpcode.GeChar:
      case ResolvedOpcode.Superset:
      case ResolvedOpcode.MultiSuperset:
        return Opcode.Ge;

      case ResolvedOpcode.Gt:
      case ResolvedOpcode.GtChar:
      case ResolvedOpcode.ProperSuperset:
      case ResolvedOpcode.ProperMultiSubset:
      case ResolvedOpcode.RankGt:
        return Opcode.Gt;

      case ResolvedOpcode.LeftShift:
        return Opcode.LeftShift;

      case ResolvedOpcode.RightShift:
        return Opcode.RightShift;

      case ResolvedOpcode.Add:
      case ResolvedOpcode.Union:
      case ResolvedOpcode.MultiSetUnion:
      case ResolvedOpcode.MapMerge:
      case ResolvedOpcode.Concat:
        return Opcode.Add;

      case ResolvedOpcode.Sub:
      case ResolvedOpcode.SetDifference:
      case ResolvedOpcode.MultiSetDifference:
      case ResolvedOpcode.MapSubtraction:
        return Opcode.Sub;

      case ResolvedOpcode.Mul:
      case ResolvedOpcode.Intersection:
      case ResolvedOpcode.MultiSetIntersection:
        return Opcode.Mul;

      case ResolvedOpcode.Div: return Opcode.Div;
      case ResolvedOpcode.Mod: return Opcode.Mod;

      case ResolvedOpcode.BitwiseAnd: return Opcode.BitwiseAnd;
      case ResolvedOpcode.BitwiseOr: return Opcode.BitwiseOr;
      case ResolvedOpcode.BitwiseXor: return Opcode.BitwiseXor;

      case ResolvedOpcode.Disjoint:
      case ResolvedOpcode.MultiSetDisjoint:
        return Opcode.Disjoint;

      case ResolvedOpcode.InSet:
      case ResolvedOpcode.InMultiSet:
      case ResolvedOpcode.InSeq:
      case ResolvedOpcode.InMap:
        return Opcode.In;

      case ResolvedOpcode.NotInSet:
      case ResolvedOpcode.NotInMultiSet:
      case ResolvedOpcode.NotInSeq:
      case ResolvedOpcode.NotInMap:
        return Opcode.NotIn;

      case ResolvedOpcode.LessThanLimit:  // not expected here (but if it were, the same case as Lt could perhaps be used)
      default:
        Contract.Assert(false);  // unexpected ResolvedOpcode
        return Opcode.Add;  // please compiler
    }
  }

  public static string OpcodeString(Opcode op) {
    Contract.Ensures(Contract.Result<string>() != null);

    switch (op) {
      case Opcode.Iff:
        return "<==>";
      case Opcode.Imp:
        return "==>";
      case Opcode.Exp:
        return "<==";
      case Opcode.And:
        return "&&";
      case Opcode.Or:
        return "||";
      case Opcode.Eq:
        return "==";
      case Opcode.Lt:
        return "<";
      case Opcode.Gt:
        return ">";
      case Opcode.Le:
        return "<=";
      case Opcode.Ge:
        return ">=";
      case Opcode.Neq:
        return "!=";
      case Opcode.Disjoint:
        return "!!";
      case Opcode.In:
        return "in";
      case Opcode.NotIn:
        return "!in";
      case Opcode.LeftShift:
        return "<<";
      case Opcode.RightShift:
        return ">>";
      case Opcode.Add:
        return "+";
      case Opcode.Sub:
        return "-";
      case Opcode.Mul:
        return "*";
      case Opcode.Div:
        return "/";
      case Opcode.Mod:
        return "%";
      case Opcode.BitwiseAnd:
        return "&";
      case Opcode.BitwiseOr:
        return "|";
      case Opcode.BitwiseXor:
        return "^";
      default:
        Contract.Assert(false);
        throw new cce.UnreachableException();  // unexpected operator
    }
  }
  public Expression E0;
  public Expression E1;
  public enum AccumulationOperand { None, Left, Right }
  public AccumulationOperand AccumulatesForTailRecursion = AccumulationOperand.None; // set by Resolver
  [FilledInDuringResolution] public bool InCompiledContext;

  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(E0 != null);
    Contract.Invariant(E1 != null);
  }

  public BinaryExpr(IToken tok, Opcode op, Expression e0, Expression e1)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(e0 != null);
    Contract.Requires(e1 != null);
    this.Op = op;
    this.E0 = e0;
    this.E1 = e1;
  }

  /// <summary>
  /// Returns a resolved binary expression
  /// </summary>
  public BinaryExpr(IToken tok, BinaryExpr.ResolvedOpcode rop, Expression e0, Expression e1)
    : this(tok, BinaryExpr.ResolvedOp2SyntacticOp(rop), e0, e1) {
    ResolvedOp = rop;
    switch (rop) {
      case ResolvedOpcode.EqCommon:
      case ResolvedOpcode.NeqCommon:
      case ResolvedOpcode.Lt:
      case ResolvedOpcode.LessThanLimit:
      case ResolvedOpcode.Le:
      case ResolvedOpcode.Ge:
      case ResolvedOpcode.Gt:
      case ResolvedOpcode.LtChar:
      case ResolvedOpcode.LeChar:
      case ResolvedOpcode.GeChar:
      case ResolvedOpcode.GtChar:
      case ResolvedOpcode.SetEq:
      case ResolvedOpcode.SetNeq:
      case ResolvedOpcode.ProperSubset:
      case ResolvedOpcode.Subset:
      case ResolvedOpcode.Superset:
      case ResolvedOpcode.ProperSuperset:
      case ResolvedOpcode.Disjoint:
      case ResolvedOpcode.InSet:
      case ResolvedOpcode.NotInSet:
      case ResolvedOpcode.MultiSetEq:
      case ResolvedOpcode.MultiSetNeq:
      case ResolvedOpcode.MultiSubset:
      case ResolvedOpcode.MultiSuperset:
      case ResolvedOpcode.ProperMultiSubset:
      case ResolvedOpcode.ProperMultiSuperset:
      case ResolvedOpcode.MultiSetDisjoint:
      case ResolvedOpcode.InMultiSet:
      case ResolvedOpcode.NotInMultiSet:
      case ResolvedOpcode.SeqEq:
      case ResolvedOpcode.SeqNeq:
      case ResolvedOpcode.ProperPrefix:
      case ResolvedOpcode.Prefix:
      case ResolvedOpcode.InSeq:
      case ResolvedOpcode.NotInSeq:
      case ResolvedOpcode.MapEq:
      case ResolvedOpcode.MapNeq:
      case ResolvedOpcode.InMap:
      case ResolvedOpcode.NotInMap:
      case ResolvedOpcode.RankLt:
      case ResolvedOpcode.RankGt:
        Type = Type.Bool;
        break;
      default:
        Type = e0.Type;
        break;
    }
  }

  public override IEnumerable<Expression> SubExpressions {
    get {
      yield return E0;
      yield return E1;
    }
  }
}

public class TernaryExpr : Expression {
  public readonly Opcode Op;
  public readonly Expression E0;
  public readonly Expression E1;
  public readonly Expression E2;
  public enum Opcode { /*SOON: IfOp,*/ PrefixEqOp, PrefixNeqOp }
  public static readonly bool PrefixEqUsesNat = false;  // "k" is either a "nat" or an "ORDINAL"
  public TernaryExpr(IToken tok, Opcode op, Expression e0, Expression e1, Expression e2)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(e0 != null);
    Contract.Requires(e1 != null);
    Contract.Requires(e2 != null);
    Op = op;
    E0 = e0;
    E1 = e1;
    E2 = e2;
  }

  public override IEnumerable<Expression> SubExpressions {
    get {
      yield return E0;
      yield return E1;
      yield return E2;
    }
  }
}

public class LetExpr : Expression, IAttributeBearingDeclaration, IBoundVarsBearingExpression {
  public readonly List<CasePattern<BoundVar>> LHSs;
  public readonly List<Expression> RHSs;
  public readonly Expression Body;
  public readonly bool Exact;  // Exact==true means a regular let expression; Exact==false means an assign-such-that expression
  public readonly Attributes Attributes;
  Attributes IAttributeBearingDeclaration.Attributes => Attributes;
  [FilledInDuringResolution] public List<ComprehensionExpr.BoundedPool> Constraint_Bounds;  // null for Exact=true and for when expression is in a ghost context
  // invariant Constraint_Bounds == null || Constraint_Bounds.Count == BoundVars.Count;
  private Expression translationDesugaring;  // filled in during translation, lazily; to be accessed only via Translation.LetDesugaring; always null when Exact==true
  private Translator lastTranslatorUsed; // avoid clashing desugaring between translators

  public IToken BodyStartTok = Token.NoToken;
  public IToken BodyEndTok = Token.NoToken;
  IToken IRegion.BodyStartTok { get { return BodyStartTok; } }
  IToken IRegion.BodyEndTok { get { return BodyEndTok; } }

  public void setTranslationDesugaring(Translator trans, Expression expr) {
    lastTranslatorUsed = trans;
    translationDesugaring = expr;
  }

  public Expression getTranslationDesugaring(Translator trans) {
    if (lastTranslatorUsed == trans) {
      return translationDesugaring;
    } else {
      return null;
    }
  }

  public LetExpr(IToken tok, List<CasePattern<BoundVar>> lhss, List<Expression> rhss, Expression body, bool exact, Attributes attrs = null)
    : base(tok) {
    LHSs = lhss;
    RHSs = rhss;
    Body = body;
    Exact = exact;
    Attributes = attrs;
  }
  public override IEnumerable<Expression> SubExpressions {
    get {
      foreach (var e in Attributes.SubExpressions(Attributes)) {
        yield return e;
      }
      foreach (var rhs in RHSs) {
        yield return rhs;
      }
      yield return Body;
    }
  }

  public override IEnumerable<Type> ComponentTypes => BoundVars.Select(bv => bv.Type);

  public IEnumerable<BoundVar> BoundVars {
    get {
      foreach (var lhs in LHSs) {
        foreach (var bv in lhs.Vars) {
          yield return bv;
        }
      }
    }
  }

  public IEnumerable<BoundVar> AllBoundVars => BoundVars;
}

public class LetOrFailExpr : ConcreteSyntaxExpression {
  public readonly CasePattern<BoundVar>/*?*/ Lhs; // null means void-error handling: ":- E; F", non-null means "var pat :- E; F"
  public readonly Expression Rhs;
  public readonly Expression Body;

  public LetOrFailExpr(IToken tok, CasePattern<BoundVar>/*?*/ lhs, Expression rhs, Expression body) : base(tok) {
    Lhs = lhs;
    Rhs = rhs;
    Body = body;
  }
}

/// <summary>
/// A ComprehensionExpr has the form:
///   BINDER { x [: Type] [<- Domain] [Attributes] [| Range] } [:: Term(x)]
/// Where BINDER is currently "forall", "exists", "iset"/"set", or "imap"/"map".
///
/// Quantifications used to only support a single range, but now each
/// quantified variable can have a range attached.
/// The overall Range is now filled in by the parser by extracting any implicit
/// "x in Domain" constraints and per-variable Range constraints into a single conjunct.
///
/// The Term is optional if the expression only has one quantified variable,
/// but required otherwise.
///
/// LambdaExpr also inherits from this base class but isn't really a comprehension,
/// and should be considered implementation inheritance.
/// </summary>
public abstract class ComprehensionExpr : Expression, IAttributeBearingDeclaration, IBoundVarsBearingExpression {
  public virtual string WhatKind => "comprehension";
  public readonly List<BoundVar> BoundVars;
  public readonly Expression Range;
  private Expression term;
  public Expression Term { get { return term; } }
  public IEnumerable<BoundVar> AllBoundVars => BoundVars;

  public IToken BodyStartTok = Token.NoToken;
  public IToken BodyEndTok = Token.NoToken;
  IToken IRegion.BodyStartTok { get { return BodyStartTok; } }
  IToken IRegion.BodyEndTok { get { return BodyEndTok; } }

  public void UpdateTerm(Expression newTerm) {
    term = newTerm;
  }

  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(BoundVars != null);
    Contract.Invariant(Term != null);
  }

  public Attributes Attributes;
  Attributes IAttributeBearingDeclaration.Attributes => Attributes;

  public abstract class BoundedPool {
    [Flags]
    public enum PoolVirtues { None = 0, Finite = 1, Enumerable = 2, IndependentOfAlloc = 4, IndependentOfAlloc_or_ExplicitAlloc = 8 }
    public abstract PoolVirtues Virtues { get; }
    /// <summary>
    /// A higher preference is better.
    /// A preference below 2 is a last-resort bounded pool. Bounds discovery will not consider
    /// such a pool to be final until there are no other choices.
    ///
    /// For easy reference, here is the BoundedPool hierarchy and their preference levels:
    ///
    /// 0: AllocFreeBoundedPool
    /// 0: ExplicitAllocatedBoundedPool
    /// 0: SpecialAllocIndependenceAllocatedBoundedPool
    /// 0: OlderBoundedPool
    ///
    /// 1: WiggleWaggleBound
    ///
    /// 2: SuperSetBoundedPool
    /// 2: DatatypeInclusionBoundedPool
    ///
    /// 3: SubSetBoundedPool
    ///
    /// 4: IntBoundedPool with one bound
    /// 5: IntBoundedPool with both bounds
    /// 5: CharBoundedPool
    ///
    /// 8: DatatypeBoundedPool
    ///
    /// 10: CollectionBoundedPool
    ///     - SetBoundedPool
    ///     - MapBoundedPool
    ///     - SeqBoundedPool
    ///
    /// 14: BoolBoundedPool
    ///
    /// 15: ExactBoundedPool
    /// </summary>
    public abstract int Preference(); // higher is better

    public static BoundedPool GetBest(List<BoundedPool> bounds, PoolVirtues requiredVirtues) {
      Contract.Requires(bounds != null);
      bounds = CombineIntegerBounds(bounds);
      BoundedPool best = null;
      foreach (var bound in bounds) {
        if ((bound.Virtues & requiredVirtues) == requiredVirtues) {
          if (best == null || bound.Preference() > best.Preference()) {
            best = bound;
          }
        }
      }
      return best;
    }
    public static List<VT> MissingBounds<VT>(List<VT> vars, List<BoundedPool> bounds, PoolVirtues requiredVirtues = PoolVirtues.None) where VT : IVariable {
      Contract.Requires(vars != null);
      Contract.Requires(bounds == null || vars.Count == bounds.Count);
      Contract.Ensures(Contract.Result<List<VT>>() != null);
      var missing = new List<VT>();
      for (var i = 0; i < vars.Count; i++) {
        if (bounds == null || bounds[i] == null || (bounds[i].Virtues & requiredVirtues) != requiredVirtues) {
          missing.Add(vars[i]);
        }
      }
      return missing;
    }
    public static List<bool> HasBounds(List<BoundedPool> bounds, PoolVirtues requiredVirtues = PoolVirtues.None) {
      Contract.Requires(bounds != null);
      Contract.Ensures(Contract.Result<List<bool>>() != null);
      Contract.Ensures(Contract.Result<List<bool>>().Count == bounds.Count);
      return bounds.ConvertAll(bound => bound != null && (bound.Virtues & requiredVirtues) == requiredVirtues);
    }
    static List<BoundedPool> CombineIntegerBounds(List<BoundedPool> bounds) {
      var lowerBounds = new List<IntBoundedPool>();
      var upperBounds = new List<IntBoundedPool>();
      var others = new List<BoundedPool>();
      foreach (var b in bounds) {
        var ib = b as IntBoundedPool;
        if (ib != null && ib.UpperBound == null) {
          lowerBounds.Add(ib);
        } else if (ib != null && ib.LowerBound == null) {
          upperBounds.Add(ib);
        } else {
          others.Add(b);
        }
      }
      // pair up the bounds
      var n = Math.Min(lowerBounds.Count, upperBounds.Count);
      for (var i = 0; i < n; i++) {
        others.Add(new IntBoundedPool(lowerBounds[i].LowerBound, upperBounds[i].UpperBound));
      }
      for (var i = n; i < lowerBounds.Count; i++) {
        others.Add(lowerBounds[i]);
      }
      for (var i = n; i < upperBounds.Count; i++) {
        others.Add(upperBounds[i]);
      }
      return others;
    }
  }
  public class ExactBoundedPool : BoundedPool {
    public readonly Expression E;
    public ExactBoundedPool(Expression e) {
      Contract.Requires(e != null);
      E = e;
    }
    public override PoolVirtues Virtues => PoolVirtues.Finite | PoolVirtues.Enumerable | PoolVirtues.IndependentOfAlloc | PoolVirtues.IndependentOfAlloc_or_ExplicitAlloc;
    public override int Preference() => 15;  // the best of all bounds
  }
  public class BoolBoundedPool : BoundedPool {
    public override PoolVirtues Virtues => PoolVirtues.Finite | PoolVirtues.Enumerable | PoolVirtues.IndependentOfAlloc | PoolVirtues.IndependentOfAlloc_or_ExplicitAlloc;
    public override int Preference() => 14;
  }
  public class CharBoundedPool : BoundedPool {
    public override PoolVirtues Virtues => PoolVirtues.Finite | PoolVirtues.Enumerable | PoolVirtues.IndependentOfAlloc | PoolVirtues.IndependentOfAlloc_or_ExplicitAlloc;
    public override int Preference() => 5;
  }
  public class AllocFreeBoundedPool : BoundedPool {
    public Type Type;
    public AllocFreeBoundedPool(Type t) {
      Type = t;
    }
    public override PoolVirtues Virtues {
      get {
        if (Type.IsRefType) {
          return PoolVirtues.Finite | PoolVirtues.IndependentOfAlloc | PoolVirtues.IndependentOfAlloc_or_ExplicitAlloc;
        } else {
          return PoolVirtues.IndependentOfAlloc | PoolVirtues.IndependentOfAlloc_or_ExplicitAlloc;
        }
      }
    }
    public override int Preference() => 0;
  }
  public class ExplicitAllocatedBoundedPool : BoundedPool {
    public ExplicitAllocatedBoundedPool() {
    }
    public override PoolVirtues Virtues => PoolVirtues.Finite | PoolVirtues.IndependentOfAlloc_or_ExplicitAlloc;
    public override int Preference() => 0;
  }
  public class SpecialAllocIndependenceAllocatedBoundedPool : BoundedPool {
    public SpecialAllocIndependenceAllocatedBoundedPool() {
    }
    public override PoolVirtues Virtues => PoolVirtues.IndependentOfAlloc_or_ExplicitAlloc;
    public override int Preference() => 0;
  }
  public class IntBoundedPool : BoundedPool {
    public readonly Expression LowerBound;
    public readonly Expression UpperBound;
    public IntBoundedPool(Expression lowerBound, Expression upperBound) {
      Contract.Requires(lowerBound != null || upperBound != null);
      LowerBound = lowerBound;
      UpperBound = upperBound;
    }
    public override PoolVirtues Virtues {
      get {
        if (LowerBound != null && UpperBound != null) {
          return PoolVirtues.Finite | PoolVirtues.Enumerable | PoolVirtues.IndependentOfAlloc | PoolVirtues.IndependentOfAlloc_or_ExplicitAlloc;
        } else {
          return PoolVirtues.Enumerable | PoolVirtues.IndependentOfAlloc | PoolVirtues.IndependentOfAlloc_or_ExplicitAlloc;
        }
      }
    }
    public override int Preference() => LowerBound != null && UpperBound != null ? 5 : 4;
  }
  public abstract class CollectionBoundedPool : BoundedPool {
    public readonly Type BoundVariableType;
    public readonly Type CollectionElementType;
    public readonly bool IsFiniteCollection;

    public CollectionBoundedPool(Type bvType, Type collectionElementType, bool isFiniteCollection) {
      Contract.Requires(bvType != null);
      Contract.Requires(collectionElementType != null);

      BoundVariableType = bvType;
      CollectionElementType = collectionElementType;
      IsFiniteCollection = isFiniteCollection;
    }

    public override PoolVirtues Virtues {
      get {
        var v = PoolVirtues.IndependentOfAlloc | PoolVirtues.IndependentOfAlloc_or_ExplicitAlloc;
        if (IsFiniteCollection) {
          v |= PoolVirtues.Finite;
          if (CollectionElementType.IsTestableToBe(BoundVariableType)) {
            v |= PoolVirtues.Enumerable;
          }
        }
        return v;
      }
    }
    public override int Preference() => 10;
  }
  public class SetBoundedPool : CollectionBoundedPool {
    public readonly Expression Set;

    public SetBoundedPool(Expression set, Type bvType, Type collectionElementType, bool isFiniteCollection)
      : base(bvType, collectionElementType, isFiniteCollection) {
      Contract.Requires(set != null);
      Contract.Requires(bvType != null);
      Contract.Requires(collectionElementType != null);
      Set = set;
    }
  }
  public class SubSetBoundedPool : BoundedPool {
    public readonly Expression UpperBound;
    public readonly bool IsFiniteCollection;
    public SubSetBoundedPool(Expression set, bool isFiniteCollection) {
      UpperBound = set;
      IsFiniteCollection = isFiniteCollection;
    }
    public override PoolVirtues Virtues {
      get {
        if (IsFiniteCollection) {
          return PoolVirtues.Finite | PoolVirtues.Enumerable | PoolVirtues.IndependentOfAlloc | PoolVirtues.IndependentOfAlloc_or_ExplicitAlloc;
        } else {
          // it's still enumerable, because at run time, all sets are finite after all
          return PoolVirtues.Enumerable | PoolVirtues.IndependentOfAlloc | PoolVirtues.IndependentOfAlloc_or_ExplicitAlloc;
        }
      }
    }
    public override int Preference() => 3;
  }
  public class SuperSetBoundedPool : BoundedPool {
    public readonly Expression LowerBound;
    public SuperSetBoundedPool(Expression set) { LowerBound = set; }
    public override int Preference() => 2;
    public override PoolVirtues Virtues {
      get {
        if (LowerBound.Type.MayInvolveReferences) {
          return PoolVirtues.None;
        } else {
          return PoolVirtues.IndependentOfAlloc | PoolVirtues.IndependentOfAlloc_or_ExplicitAlloc;
        }
      }
    }
  }
  public class MultiSetBoundedPool : CollectionBoundedPool {
    public readonly Expression MultiSet;

    public MultiSetBoundedPool(Expression multiset, Type bvType, Type collectionElementType)
      : base(bvType, collectionElementType, true) {
      Contract.Requires(multiset != null);
      Contract.Requires(bvType != null);
      Contract.Requires(collectionElementType != null);
      MultiSet = multiset;
    }
  }
  public class MapBoundedPool : CollectionBoundedPool {
    public readonly Expression Map;

    public MapBoundedPool(Expression map, Type bvType, Type collectionElementType, bool isFiniteCollection)
      : base(bvType, collectionElementType, isFiniteCollection) {
      Contract.Requires(map != null);
      Contract.Requires(bvType != null);
      Contract.Requires(collectionElementType != null);
      Map = map;
    }
  }
  public class SeqBoundedPool : CollectionBoundedPool {
    public readonly Expression Seq;

    public SeqBoundedPool(Expression seq, Type bvType, Type collectionElementType)
      : base(bvType, collectionElementType, true) {
      Contract.Requires(seq != null);
      Contract.Requires(bvType != null);
      Contract.Requires(collectionElementType != null);
      Seq = seq;
    }
  }
  public class DatatypeBoundedPool : BoundedPool {
    public readonly DatatypeDecl Decl;

    public DatatypeBoundedPool(DatatypeDecl d) {
      Contract.Requires(d != null);
      Decl = d;
    }
    public override PoolVirtues Virtues => PoolVirtues.Finite | PoolVirtues.Enumerable | PoolVirtues.IndependentOfAlloc | PoolVirtues.IndependentOfAlloc_or_ExplicitAlloc;
    public override int Preference() => 8;
  }
  public class DatatypeInclusionBoundedPool : BoundedPool {
    public readonly bool IsIndDatatype;
    public DatatypeInclusionBoundedPool(bool isIndDatatype) : base() { IsIndDatatype = isIndDatatype; }
    public override PoolVirtues Virtues => (IsIndDatatype ? PoolVirtues.Finite : PoolVirtues.None) | PoolVirtues.IndependentOfAlloc | PoolVirtues.IndependentOfAlloc_or_ExplicitAlloc;
    public override int Preference() => 2;
  }

  public class OlderBoundedPool : BoundedPool {
    public OlderBoundedPool() {
    }
    public override PoolVirtues Virtues => PoolVirtues.IndependentOfAlloc | PoolVirtues.IndependentOfAlloc_or_ExplicitAlloc;
    public override int Preference() => 0;
  }

  [FilledInDuringResolution] public List<BoundedPool> Bounds;
  // invariant Bounds == null || Bounds.Count == BoundVars.Count;

  public List<BoundVar> UncompilableBoundVars() {
    Contract.Ensures(Contract.Result<List<BoundVar>>() != null);
    var v = BoundedPool.PoolVirtues.Finite | BoundedPool.PoolVirtues.Enumerable;
    return ComprehensionExpr.BoundedPool.MissingBounds(BoundVars, Bounds, v);
  }

  public ComprehensionExpr(IToken tok, IToken endTok, List<BoundVar> bvars, Expression range, Expression term, Attributes attrs)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(cce.NonNullElements(bvars));
    Contract.Requires(term != null);

    this.BoundVars = bvars;
    this.Range = range;
    this.UpdateTerm(term);
    this.Attributes = attrs;
    this.BodyStartTok = tok;
    this.BodyEndTok = endTok;
  }

  public override IEnumerable<Expression> SubExpressions {
    get {
      foreach (var e in Attributes.SubExpressions(Attributes)) {
        yield return e;
      }
      if (Range != null) { yield return Range; }
      yield return Term;
    }
  }

  public override IEnumerable<Type> ComponentTypes => BoundVars.Select(bv => bv.Type);
}

public abstract class QuantifierExpr : ComprehensionExpr, TypeParameter.ParentType {
  public override string WhatKind => "quantifier";

  private readonly int UniqueId;
  private static int currentQuantId = -1;

  protected virtual BinaryExpr.ResolvedOpcode SplitResolvedOp { get { return BinaryExpr.ResolvedOpcode.Or; } }

  private Expression SplitQuantifierToExpression() {
    Contract.Requires(SplitQuantifier != null && SplitQuantifier.Any());
    Expression accumulator = SplitQuantifier[0];
    for (int tid = 1; tid < SplitQuantifier.Count; tid++) {
      accumulator = new BinaryExpr(Term.tok, SplitResolvedOp, accumulator, SplitQuantifier[tid]);
    }
    return accumulator;
  }

  private List<Expression> _SplitQuantifier;
  public List<Expression> SplitQuantifier {
    get {
      return _SplitQuantifier;
    }
    set {
      Contract.Assert(!value.Contains(this)); // don't let it put into its own split quantifiers.
      _SplitQuantifier = value;
      SplitQuantifierExpression = SplitQuantifierToExpression();
    }
  }

  internal Expression SplitQuantifierExpression { get; private set; }

  static int FreshQuantId() {
    return System.Threading.Interlocked.Increment(ref currentQuantId);
  }

  public string FullName {
    get {
      return "q$" + UniqueId;
    }
  }

  public String Refresh(string prefix, FreshIdGenerator idGen) {
    return idGen.FreshId(prefix);
  }

  public TypeParameter Refresh(TypeParameter p, FreshIdGenerator idGen) {
    var cp = new TypeParameter(p.tok, idGen.FreshId(p.Name + "#"), p.VarianceSyntax, p.Characteristics);
    cp.Parent = this;
    return cp;
  }
  public QuantifierExpr(IToken tok, IToken endTok, List<BoundVar> bvars, Expression range, Expression term, Attributes attrs)
    : base(tok, endTok, bvars, range, term, attrs) {
    Contract.Requires(tok != null);
    Contract.Requires(cce.NonNullElements(bvars));
    Contract.Requires(term != null);
    this.UniqueId = FreshQuantId();
  }

  public virtual Expression LogicalBody(bool bypassSplitQuantifier = false) {
    // Don't call this on a quantifier with a Split clause: it's not a real quantifier. The only exception is the Compiler.
    Contract.Requires(bypassSplitQuantifier || SplitQuantifier == null);
    throw new cce.UnreachableException(); // This body is just here for the "Requires" clause
  }

  public override IEnumerable<Expression> SubExpressions {
    get {
      if (SplitQuantifier == null) {
        foreach (var e in base.SubExpressions) {
          yield return e;
        }
      } else {
        foreach (var e in Attributes.SubExpressions(Attributes)) {
          yield return e;
        }
        foreach (var e in SplitQuantifier) {
          yield return e;
        }
      }
    }
  }
}

public class ForallExpr : QuantifierExpr {
  public override string WhatKind => "forall expression";
  protected override BinaryExpr.ResolvedOpcode SplitResolvedOp { get { return BinaryExpr.ResolvedOpcode.And; } }

  public ForallExpr(IToken tok, IToken endTok, List<BoundVar> bvars, Expression range, Expression term, Attributes attrs)
    : base(tok, endTok, bvars, range, term, attrs) {
    Contract.Requires(cce.NonNullElements(bvars));
    Contract.Requires(tok != null);
    Contract.Requires(term != null);
  }
  public override Expression LogicalBody(bool bypassSplitQuantifier = false) {
    if (Range == null) {
      return Term;
    }
    var body = new BinaryExpr(Term.tok, BinaryExpr.Opcode.Imp, Range, Term);
    body.ResolvedOp = BinaryExpr.ResolvedOpcode.Imp;
    body.Type = Term.Type;
    return body;
  }
}

public class ExistsExpr : QuantifierExpr {
  public override string WhatKind => "exists expression";
  protected override BinaryExpr.ResolvedOpcode SplitResolvedOp { get { return BinaryExpr.ResolvedOpcode.Or; } }

  public ExistsExpr(IToken tok, IToken endTok, List<BoundVar> bvars, Expression range, Expression term, Attributes attrs)
    : base(tok, endTok, bvars, range, term, attrs) {
    Contract.Requires(cce.NonNullElements(bvars));
    Contract.Requires(tok != null);
    Contract.Requires(term != null);
  }
  public override Expression LogicalBody(bool bypassSplitQuantifier = false) {
    if (Range == null) {
      return Term;
    }
    var body = new BinaryExpr(Term.tok, BinaryExpr.Opcode.And, Range, Term);
    body.ResolvedOp = BinaryExpr.ResolvedOpcode.And;
    body.Type = Term.Type;
    return body;
  }
}

public class SetComprehension : ComprehensionExpr {
  public override string WhatKind => "set comprehension";

  public readonly bool Finite;
  public readonly bool TermIsImplicit;  // records the given syntactic form
  public bool TermIsSimple {
    get {
      var term = Term as IdentifierExpr;
      var r = term != null && BoundVars.Count == 1 && BoundVars[0].Name == term.Name;
      Contract.Assert(!TermIsImplicit || r);  // TermIsImplicit ==> r
      Contract.Assert(!r || term.Var == null || term.Var == BoundVars[0]);  // if the term is simple and it has been resolved, then it should have resolved to BoundVars[0]
      return r;
    }
  }

  public SetComprehension(IToken tok, IToken endTok, bool finite, List<BoundVar> bvars, Expression range, Expression/*?*/ term, Attributes attrs)
    : base(tok, endTok, bvars, range, term ?? new IdentifierExpr(tok, bvars[0].Name), attrs) {
    Contract.Requires(tok != null);
    Contract.Requires(cce.NonNullElements(bvars));
    Contract.Requires(1 <= bvars.Count);
    Contract.Requires(range != null);
    Contract.Requires(term != null || bvars.Count == 1);

    TermIsImplicit = term == null;
    Finite = finite;
  }
}
public class MapComprehension : ComprehensionExpr {
  public override string WhatKind => "map comprehension";

  public readonly bool Finite;
  public readonly Expression TermLeft;

  public List<Boogie.Function> ProjectionFunctions;  // filled in during translation (and only for general map comprehensions where "TermLeft != null")

  public MapComprehension(IToken tok, IToken endTok, bool finite, List<BoundVar> bvars, Expression range, Expression/*?*/ termLeft, Expression termRight, Attributes attrs)
    : base(tok, endTok, bvars, range, termRight, attrs) {
    Contract.Requires(tok != null);
    Contract.Requires(cce.NonNullElements(bvars));
    Contract.Requires(1 <= bvars.Count);
    Contract.Requires(range != null);
    Contract.Requires(termRight != null);
    Contract.Requires(termLeft != null || bvars.Count == 1);

    Finite = finite;
    TermLeft = termLeft;
  }

  /// <summary>
  /// IsGeneralMapComprehension returns true for general map comprehensions.
  /// In other words, it returns false if either no TermLeft was given or if
  /// the given TermLeft is the sole bound variable.
  /// This property getter requires that the expression has been successfully
  /// resolved.
  /// </summary>
  public bool IsGeneralMapComprehension {
    get {
      Contract.Requires(WasResolved());
      if (TermLeft == null) {
        return false;
      } else if (BoundVars.Count != 1) {
        return true;
      }
      var lhs = StripParens(TermLeft).Resolved;
      if (lhs is IdentifierExpr ide && ide.Var == BoundVars[0]) {
        // TermLeft is the sole bound variable, so this is the same as
        // if TermLeft wasn't given at all
        return false;
      }
      return true;
    }
  }

  public override IEnumerable<Expression> SubExpressions {
    get {
      foreach (var e in Attributes.SubExpressions(Attributes)) {
        yield return e;
      }
      if (Range != null) { yield return Range; }
      if (TermLeft != null) { yield return TermLeft; }
      yield return Term;
    }
  }
}

public class LambdaExpr : ComprehensionExpr {
  public override string WhatKind => "lambda";

  public readonly List<FrameExpression> Reads;

  public LambdaExpr(IToken tok, IToken endTok, List<BoundVar> bvars, Expression requires, List<FrameExpression> reads, Expression body)
    : base(tok, endTok, bvars, requires, body, null) {
    Contract.Requires(reads != null);
    Reads = reads;
  }

  // Synonym
  public Expression Body {
    get {
      return Term;
    }
  }

  public override IEnumerable<Expression> SubExpressions {
    get {
      yield return Term;
      if (Range != null) {
        yield return Range;
      }
      foreach (var read in Reads) {
        yield return read.E;
      }
    }
  }

}

public class WildcardExpr : Expression {  // a WildcardExpr can occur only in reads clauses and a loop's decreases clauses (with different meanings)
  public WildcardExpr(IToken tok)
    : base(tok) {
    Contract.Requires(tok != null);
  }
}

/// <summary>
/// A StmtExpr has the form S;E where S is a statement (from a restricted set) and E is an expression.
/// The expression S;E evaluates to whatever E evaluates to, but its well-formedness comes down to
/// executing S (which itself must be well-formed) and then checking the well-formedness of E.
/// </summary>
public class StmtExpr : Expression {
  public readonly Statement S;
  public readonly Expression E;
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(S != null);
    Contract.Invariant(E != null);
  }

  public StmtExpr(IToken tok, Statement stmt, Expression expr)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(stmt != null);
    Contract.Requires(expr != null);
    S = stmt;
    E = expr;
  }
  public override IEnumerable<Expression> SubExpressions {
    get {
      // Note:  A StmtExpr is unusual in that it contains a statement.  For now, callers
      // of SubExpressions need to be aware of this and handle it specially.
      yield return E;
    }
  }

  /// <summary>
  /// Returns a conclusion that S gives rise to, that is, something that is known after
  /// S is executed.
  /// This method should be called only after successful resolution of the expression.
  /// </summary>
  public Expression GetSConclusion() {
    // this is one place where we actually investigate what kind of statement .S is
    if (S is PredicateStmt) {
      var s = (PredicateStmt)S;
      return s.Expr;
    } else if (S is CalcStmt) {
      var s = (CalcStmt)S;
      return s.Result;
    } else if (S is RevealStmt) {
      return new LiteralExpr(tok, true);  // one could use the definition axiom or the referenced labeled assertions, but "true" is conservative and much simpler :)
    } else if (S is UpdateStmt) {
      return new LiteralExpr(tok, true);  // one could use the postcondition of the method, suitably instantiated, but "true" is conservative and much simpler :)
    } else {
      Contract.Assert(false); throw new cce.UnreachableException();  // unexpected statement
    }
  }
}

public class ITEExpr : Expression {
  public readonly bool IsBindingGuard;
  public readonly Expression Test;
  public readonly Expression Thn;
  public readonly Expression Els;
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(Test != null);
    Contract.Invariant(Thn != null);
    Contract.Invariant(Els != null);
  }

  public ITEExpr(IToken tok, bool isBindingGuard, Expression test, Expression thn, Expression els)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(test != null);
    Contract.Requires(thn != null);
    Contract.Requires(els != null);
    this.IsBindingGuard = isBindingGuard;
    this.Test = test;
    this.Thn = thn;
    this.Els = els;
  }

  public override IEnumerable<Expression> SubExpressions {
    get {
      yield return Test;
      yield return Thn;
      yield return Els;
    }
  }
}

public class MatchExpr : Expression {  // a MatchExpr is an "extended expression" and is only allowed in certain places
  private Expression source;
  private List<MatchCaseExpr> cases;
  public readonly MatchingContext Context;
  [FilledInDuringResolution] public readonly List<DatatypeCtor> MissingCases = new List<DatatypeCtor>();
  public readonly bool UsesOptionalBraces;
  public MatchExpr OrigUnresolved;  // the resolver makes this clone of the MatchExpr before it starts desugaring it

  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(Source != null);
    Contract.Invariant(cce.NonNullElements(Cases));
    Contract.Invariant(cce.NonNullElements(MissingCases));
  }

  public MatchExpr(IToken tok, Expression source, [Captured] List<MatchCaseExpr> cases, bool usesOptionalBraces, MatchingContext context = null)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(source != null);
    Contract.Requires(cce.NonNullElements(cases));
    this.source = source;
    this.cases = cases;
    this.UsesOptionalBraces = usesOptionalBraces;
    this.Context = context is null ? new HoleCtx() : context;
  }

  public Expression Source {
    get { return source; }
  }

  public List<MatchCaseExpr> Cases {
    get { return cases; }
  }

  // should only be used in desugar in resolve to change the source and cases of the matchexpr
  public void UpdateSource(Expression source) {
    this.source = source;
  }

  public void UpdateCases(List<MatchCaseExpr> cases) {
    this.cases = cases;
  }

  public override IEnumerable<INode> Children => new[] { source }.Concat<INode>(cases);

  public override IEnumerable<Expression> SubExpressions {
    get {
      yield return Source;
      foreach (var mc in cases) {
        yield return mc.Body;
      }
    }
  }

  public override IEnumerable<Type> ComponentTypes {
    get {
      foreach (var mc in cases) {
        foreach (var bv in mc.Arguments) {
          yield return bv.Type;
        }
      }
    }
  }
}

/// <summary>
/// A CasePattern is either a BoundVar or a datatype constructor with optional arguments.
/// Lexically, the CasePattern starts with an identifier.  If it continues with an open paren (as
/// indicated by Arguments being non-null), then the CasePattern is a datatype constructor.  If
/// it continues with a colon (which is indicated by Var.Type not being a proxy type), then it is
/// a BoundVar.  But if it ends with just the identifier, then resolution is required to figure out
/// which it is; in this case, Var is non-null, because this is the only place where Var.IsGhost
/// is recorded by the parser.
/// </summary>
public class CasePattern<VT> where VT : IVariable {
  public readonly IToken tok;
  public readonly string Id;
  // After successful resolution, exactly one of the following two fields is non-null.
  public DatatypeCtor Ctor;  // finalized by resolution (null if the pattern is a bound variable)
  public VT Var;  // finalized by resolution (null if the pattern is a constructor)  Invariant:  Var != null ==> Arguments == null
  public List<CasePattern<VT>> Arguments;

  [FilledInDuringResolution] public Expression Expr;  // an r-value version of the CasePattern;

  public void MakeAConstructor() {
    this.Arguments = new List<CasePattern<VT>>();
  }

  public CasePattern(IToken tok, string id, [Captured] List<CasePattern<VT>> arguments) {
    Contract.Requires(tok != null);
    Contract.Requires(id != null);
    this.tok = tok;
    Id = id;
    Arguments = arguments;
  }

  public CasePattern(IToken tok, VT bv) {
    Contract.Requires(tok != null);
    Contract.Requires(bv != null);
    this.tok = tok;
    Id = bv.Name;
    Var = bv;
  }

  /// <summary>
  /// Sets the Expr field.  Assumes the CasePattern and its arguments to have been successfully resolved, except for assigning
  /// to Expr.
  /// </summary>
  public void AssembleExpr(List<Type> dtvTypeArgs) {
    Contract.Requires(Var != null || dtvTypeArgs != null);
    if (Var != null) {
      Contract.Assert(this.Id == this.Var.Name);
      this.Expr = new IdentifierExpr(this.tok, this.Var);
    } else {
      var dtValue = new DatatypeValue(this.tok, this.Ctor.EnclosingDatatype.Name, this.Id,
        this.Arguments == null ? new List<Expression>() : this.Arguments.ConvertAll(arg => arg.Expr));
      dtValue.Ctor = this.Ctor;  // resolve here
      dtValue.InferredTypeArgs.AddRange(dtvTypeArgs);  // resolve here
      dtValue.Type = new UserDefinedType(this.tok, this.Ctor.EnclosingDatatype.Name, this.Ctor.EnclosingDatatype, dtvTypeArgs);
      this.Expr = dtValue;
    }
  }

  public IEnumerable<VT> Vars {
    get {
      if (Var != null) {
        yield return Var;
      } else {
        if (Arguments != null) {
          foreach (var arg in Arguments) {
            foreach (var bv in arg.Vars) {
              yield return bv;
            }
          }
        }
      }
    }
  }
}

public abstract class MatchCase : IHasUsages {
  public readonly IToken tok;
  [FilledInDuringResolution] public DatatypeCtor Ctor;
  public List<BoundVar> Arguments; // created by the resolver.
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(tok != null);
    Contract.Invariant(Ctor != null);
    Contract.Invariant(cce.NonNullElements(Arguments));
  }

  public MatchCase(IToken tok, DatatypeCtor ctor, [Captured] List<BoundVar> arguments) {
    Contract.Requires(tok != null);
    Contract.Requires(ctor != null);
    Contract.Requires(cce.NonNullElements(arguments));
    this.tok = tok;
    this.Ctor = ctor;
    this.Arguments = arguments;
  }

  public IToken NameToken => tok;
  public abstract IEnumerable<INode> Children { get; }
  public IEnumerable<IDeclarationOrUsage> GetResolvedDeclarations() {
    return new[] { Ctor };
  }
}

public class MatchCaseExpr : MatchCase {
  private Expression body;
  public Attributes Attributes;
  public readonly bool FromBoundVar;
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(body != null);
  }

  public override IEnumerable<INode> Children => Arguments.Concat<INode>(new[] { body });

  public MatchCaseExpr(IToken tok, DatatypeCtor ctor, bool FromBoundVar, [Captured] List<BoundVar> arguments, Expression body, Attributes attrs = null)
    : base(tok, ctor, arguments) {
    Contract.Requires(tok != null);
    Contract.Requires(ctor != null);
    Contract.Requires(cce.NonNullElements(arguments));
    Contract.Requires(body != null);
    this.body = body;
    this.Attributes = attrs;
    this.FromBoundVar = FromBoundVar;
  }

  public Expression Body {
    get { return body; }
  }

  // should only be called by resolve to reset the body of the MatchCaseExpr
  public void UpdateBody(Expression body) {
    this.body = body;
  }
}
/*
MatchingContext represents the context
in which a pattern-match takes place during pattern-matching compilation

MatchingContext is either:
1 - a HoleCtx
    standing for one of the current selectors in pattern-matching compilation
2 - A ForallCtx
    standing for a pattern-match over any expression
3 - an IdCtx of a string and a list of MatchingContext
    standing for a pattern-match over a constructor
4 - a LitCtx
    standing for a pattern-match over a constant
*/
public abstract class MatchingContext {
  public virtual MatchingContext AbstractAllHoles() {
    return this;
  }

  public MatchingContext AbstractHole() {
    return this.FillHole(new ForallCtx());
  }

  public virtual MatchingContext FillHole(MatchingContext curr) {
    return this;
  }
}

public class LitCtx : MatchingContext {
  public readonly LiteralExpr Lit;

  public LitCtx(LiteralExpr lit) {
    Contract.Requires(lit != null);
    this.Lit = lit;
  }

  public override string ToString() {
    return Printer.ExprToString(Lit);
  }
}

public class HoleCtx : MatchingContext {
  public HoleCtx() { }

  public override string ToString() {
    return "*";
  }

  public override MatchingContext AbstractAllHoles() {
    return new ForallCtx();
  }

  public override MatchingContext FillHole(MatchingContext curr) {
    return curr;
  }
}

public class ForallCtx : MatchingContext {
  public ForallCtx() { }

  public override string ToString() {
    return "_";
  }
}

public class IdCtx : MatchingContext {
  public readonly String Id;
  public readonly List<MatchingContext> Arguments;

  public IdCtx(String id, List<MatchingContext> arguments) {
    Contract.Requires(id != null);
    Contract.Requires(arguments != null); // Arguments can be empty, but shouldn't be null
    this.Id = id;
    this.Arguments = arguments;
  }

  public IdCtx(KeyValuePair<string, DatatypeCtor> ctor) {
    List<MatchingContext> arguments = Enumerable.Repeat((MatchingContext)new HoleCtx(), ctor.Value.Formals.Count).ToList();
    this.Id = ctor.Key;
    this.Arguments = arguments;
  }

  public override string ToString() {
    if (Arguments.Count == 0) {
      return Id;
    } else {
      List<string> cps = Arguments.ConvertAll<string>(x => x.ToString());
      return string.Format("{0}({1})", Id, String.Join(", ", cps));
    }
  }

  public override MatchingContext AbstractAllHoles() {
    return new IdCtx(this.Id, this.Arguments.ConvertAll<MatchingContext>(x => x.AbstractAllHoles()));
  }

  // Find the first (leftmost) occurrence of HoleCtx and replace it with curr
  // Returns false if no HoleCtx is found
  private bool ReplaceLeftmost(MatchingContext curr, out MatchingContext newcontext) {
    var newArguments = new List<MatchingContext>();
    bool foundHole = false;
    int currArgIndex = 0;

    while (!foundHole && currArgIndex < this.Arguments.Count) {
      var arg = this.Arguments.ElementAt(currArgIndex);
      switch (arg) {
        case HoleCtx _:
          foundHole = true;
          newArguments.Add(curr);
          break;
        case IdCtx argId:
          MatchingContext newarg;
          foundHole = argId.ReplaceLeftmost(curr, out newarg);
          newArguments.Add(newarg);
          break;
        default:
          newArguments.Add(arg);
          break;
      }
      currArgIndex++;
    }

    if (foundHole) {
      while (currArgIndex < this.Arguments.Count) {
        newArguments.Add(this.Arguments.ElementAt(currArgIndex));
        currArgIndex++;
      }
    }

    newcontext = new IdCtx(this.Id, newArguments);
    return foundHole;
  }

  public override MatchingContext FillHole(MatchingContext curr) {
    MatchingContext newcontext;
    ReplaceLeftmost(curr, out newcontext);
    return newcontext;
  }
}

/*
ExtendedPattern is either:
1 - A LitPattern of a LiteralExpr, representing a constant pattern
2 - An IdPattern of a string and a list of ExtendedPattern, representing either
    a bound variable or a constructor applied to n arguments or a symbolic constant
*/
public abstract class ExtendedPattern : INode {
  public readonly IToken Tok;
  public bool IsGhost;

  public ExtendedPattern(IToken tok, bool isGhost = false) {
    Contract.Requires(tok != null);
    this.Tok = tok;
    this.IsGhost = isGhost;
  }

  public abstract IEnumerable<INode> Children { get; }
}

public class DisjunctivePattern : ExtendedPattern {
  public readonly List<ExtendedPattern> Alternatives;
  public DisjunctivePattern(IToken tok, List<ExtendedPattern> alternatives, bool isGhost = false) : base(tok, isGhost) {
    Contract.Requires(alternatives != null && alternatives.Count > 0);
    this.Alternatives = alternatives;
  }

  public override IEnumerable<INode> Children => Alternatives;
}

public class LitPattern : ExtendedPattern {
  public readonly Expression OrigLit;  // the expression as parsed; typically a LiteralExpr, but could be a NegationExpression

  /// <summary>
  /// The patterns of match constructs are rewritten very early during resolution, before any type information
  /// is available. This is unfortunate. It means we can't reliably rewrite negated expressions. In Dafny, "-" followed
  /// by digits is a negative literal for integers and reals, but as unary minus for bitvectors and ORDINAL (and
  /// unary minus is not allowed for ORDINAL, so that should always give an error).
  ///
  /// Since we don't have the necessary type information at this time, we optimistically negate all numeric literals here.
  /// After type checking, we look to see if we negated something we should not have.
  ///
  /// One could imagine allowing negative bitvector literals in case patterns and treating and them as synonyms for their
  /// positive counterparts. However, since the rewriting does not know about these synonyms, it would end up splitting
  /// cases that should have been combined, which leads to incorrect code.
  ///
  /// It would be good to check for these inadvertently allowed unary expressions only in the expanded patterns. However,
  /// the rewriting of patterns turns them into "if" statements and what not, so it's not easy to identify when a literal
  /// comes from this rewrite. Luckily, when other NegationExpressions are resolved, they turn into unary minus for bitvectors
  /// and into errors for ORDINALs. Therefore, any negative bitvector or ORDINAL literal discovered later can only have
  /// come from this rewriting. So, that's where errors are generated.
  ///
  /// One more detail, after the syntactic "-0" has been negated, the result is not negative. Therefore, what the previous
  /// paragraph explained as checking for negative bitvectors and ORDINALs doesn't work for "-0". So, instead of checking
  /// for the number being negative, the later pass will check if the token associated with the literal is "-0", a condition
  /// the assignment below ensures.
  /// </summary>
  public LiteralExpr OptimisticallyDesugaredLit {
    get {
      if (OrigLit is NegationExpression neg) {
        var lit = (LiteralExpr)neg.E;
        if (lit.Value is BaseTypes.BigDec d) {
          return new LiteralExpr(neg.tok, -d);
        } else {
          var n = (BigInteger)lit.Value;
          var tok = new Token(neg.tok.line, neg.tok.col) {
            Filename = neg.tok.Filename,
            val = "-0"
          };
          return new LiteralExpr(tok, -n);
        }
      } else {
        return (LiteralExpr)OrigLit;
      }
    }
  }

  public LitPattern(IToken tok, Expression lit, bool isGhost = false) : base(tok, isGhost) {
    Contract.Requires(lit is LiteralExpr || lit is NegationExpression);
    this.OrigLit = lit;
  }

  public override string ToString() {
    return Printer.ExprToString(OrigLit);
  }

  public override IEnumerable<INode> Children => new[] { OrigLit };
}

public class IdPattern : ExtendedPattern, IHasUsages {
  public bool HasParenthesis { get; }
  public readonly String Id;
  public readonly Type Type; // This is the syntactic type, ExtendedPatterns dissapear during resolution.
  public List<ExtendedPattern> Arguments; // null if just an identifier; possibly empty argument list if a constructor call
  public LiteralExpr ResolvedLit; // null if just an identifier
  [FilledInDuringResolution]
  public DatatypeCtor Ctor;

  public bool IsWildcardPattern =>
    Arguments == null && Id.StartsWith("_");

  public void MakeAConstructor() {
    this.Arguments = new List<ExtendedPattern>();
  }

  public IdPattern(IToken tok, String id, List<ExtendedPattern> arguments, bool isGhost = false, bool hasParenthesis = false) : base(tok, isGhost) {
    Contract.Requires(id != null);
    Contract.Requires(arguments != null); // Arguments can be empty, but shouldn't be null
    HasParenthesis = hasParenthesis;
    this.Id = id;
    this.Type = new InferredTypeProxy();
    this.Arguments = arguments;
  }

  public IdPattern(IToken tok, String id, Type type, List<ExtendedPattern> arguments, bool isGhost = false) : base(tok, isGhost) {
    Contract.Requires(id != null);
    Contract.Requires(arguments != null); // Arguments can be empty, but shouldn't be null
    this.Id = id;
    this.Type = type == null ? new InferredTypeProxy() : type;
    this.Arguments = arguments;
    this.IsGhost = isGhost;
  }

  public override string ToString() {
    if (Arguments == null || Arguments.Count == 0) {
      return Id;
    } else {
      List<string> cps = Arguments.ConvertAll<string>(x => x.ToString());
      return string.Format("{0}({1})", Id, String.Join(", ", cps));
    }
  }

  public override IEnumerable<INode> Children => Arguments ?? Enumerable.Empty<INode>();
  public IEnumerable<IDeclarationOrUsage> GetResolvedDeclarations() {
    return new IDeclarationOrUsage[] { Ctor }.Where(x => x != null);
  }

  public IToken NameToken => Tok;
}

public abstract class NestedMatchCase : INode {
  public readonly IToken Tok;
  public readonly ExtendedPattern Pat;

  public NestedMatchCase(IToken tok, ExtendedPattern pat) {
    Contract.Requires(tok != null);
    Contract.Requires(pat != null);
    this.Tok = tok;
    this.Pat = pat;
  }

  public abstract IEnumerable<INode> Children { get; }
}

public class NestedMatchCaseExpr : NestedMatchCase, IAttributeBearingDeclaration {
  public readonly Expression Body;
  public Attributes Attributes;
  Attributes IAttributeBearingDeclaration.Attributes => Attributes;

  public NestedMatchCaseExpr(IToken tok, ExtendedPattern pat, Expression body, Attributes attrs) : base(tok, pat) {
    Contract.Requires(body != null);
    this.Body = body;
    this.Attributes = attrs;
  }

  public override IEnumerable<INode> Children => new INode[] { Body, Pat }.Concat(Attributes?.Args ?? Enumerable.Empty<INode>());
}

public class NestedMatchCaseStmt : NestedMatchCase, IAttributeBearingDeclaration {
  public readonly List<Statement> Body;
  public Attributes Attributes;
  Attributes IAttributeBearingDeclaration.Attributes => Attributes;
  public NestedMatchCaseStmt(IToken tok, ExtendedPattern pat, List<Statement> body) : base(tok, pat) {
    Contract.Requires(body != null);
    this.Body = body;
    this.Attributes = null;
  }
  public NestedMatchCaseStmt(IToken tok, ExtendedPattern pat, List<Statement> body, Attributes attrs) : base(tok, pat) {
    Contract.Requires(body != null);
    this.Body = body;
    this.Attributes = attrs;
  }

  public override IEnumerable<INode> Children => Body.Concat<INode>(Attributes?.Args ?? Enumerable.Empty<INode>());
}

public class NestedMatchStmt : ConcreteSyntaxStatement {
  public readonly Expression Source;
  public readonly List<NestedMatchCaseStmt> Cases;
  public readonly bool UsesOptionalBraces;

  private void InitializeAttributes() {
    // Default case for match is false
    bool splitMatch = Attributes.Contains(this.Attributes, "split");
    Attributes.ContainsBool(this.Attributes, "split", ref splitMatch);
    foreach (var c in this.Cases) {
      if (!Attributes.Contains(c.Attributes, "split")) {
        List<Expression> args = new List<Expression>();
        args.Add(new LiteralExpr(c.Tok, splitMatch));
        Attributes attrs = new Attributes("split", args, c.Attributes);
        c.Attributes = attrs;
      }
    }
  }

  public override IEnumerable<Expression> NonSpecificationSubExpressions {
    get {
      foreach (var e in base.NonSpecificationSubExpressions) {
        yield return e;
      }
      if (this.ResolvedStatement == null) {
        yield return Source;
      }
    }
  }
  public NestedMatchStmt(IToken tok, IToken endTok, Expression source, [Captured] List<NestedMatchCaseStmt> cases, bool usesOptionalBraces, Attributes attrs = null)
    : base(tok, endTok, attrs) {
    Contract.Requires(source != null);
    Contract.Requires(cce.NonNullElements(cases));
    this.Source = source;
    this.Cases = cases;
    this.UsesOptionalBraces = usesOptionalBraces;
    InitializeAttributes();
  }
}

public class NestedMatchExpr : ConcreteSyntaxExpression {
  public readonly Expression Source;
  public readonly List<NestedMatchCaseExpr> Cases;
  public readonly bool UsesOptionalBraces;
  public Attributes Attributes;

  public NestedMatchExpr(IToken tok, Expression source, [Captured] List<NestedMatchCaseExpr> cases, bool usesOptionalBraces, Attributes attrs = null) : base(tok) {
    Contract.Requires(source != null);
    Contract.Requires(cce.NonNullElements(cases));
    this.Source = source;
    this.Cases = cases;
    this.UsesOptionalBraces = usesOptionalBraces;
    this.Attributes = attrs;
  }
}

public class BoxingCastExpr : Expression {  // a BoxingCastExpr is used only as a temporary placeholding during translation
  public readonly Expression E;
  public readonly Type FromType;
  public readonly Type ToType;
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(E != null);
    Contract.Invariant(FromType != null);
    Contract.Invariant(ToType != null);
  }

  public BoxingCastExpr(Expression e, Type fromType, Type toType)
    : base(e.tok) {
    Contract.Requires(e != null);
    Contract.Requires(fromType != null);
    Contract.Requires(toType != null);

    E = e;
    FromType = fromType;
    ToType = toType;
  }

  public override IEnumerable<Expression> SubExpressions {
    get { yield return E; }
  }
}

public class UnboxingCastExpr : Expression {  // an UnboxingCastExpr is used only as a temporary placeholding during translation
  public readonly Expression E;
  public readonly Type FromType;
  public readonly Type ToType;
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(E != null);
    Contract.Invariant(FromType != null);
    Contract.Invariant(ToType != null);
  }

  public UnboxingCastExpr(Expression e, Type fromType, Type toType)
    : base(e.tok) {
    Contract.Requires(e != null);
    Contract.Requires(fromType != null);
    Contract.Requires(toType != null);

    E = e;
    FromType = fromType;
    ToType = toType;
  }

  public override IEnumerable<Expression> SubExpressions {
    get { yield return E; }
  }
}

public class AttributedExpression : IAttributeBearingDeclaration {
  public readonly Expression E;
  public readonly AssertLabel/*?*/ Label;

  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(E != null);
  }

  private Attributes attributes;
  public Attributes Attributes {
    get {
      return attributes;
    }
    set {
      attributes = value;
    }
  }

  public bool HasAttributes() {
    return Attributes != null;
  }

  public AttributedExpression(Expression e)
    : this(e, null) {
    Contract.Requires(e != null);
  }

  public AttributedExpression(Expression e, Attributes attrs) {
    Contract.Requires(e != null);
    E = e;
    Attributes = attrs;
  }

  public AttributedExpression(Expression e, AssertLabel/*?*/ label, Attributes attrs) {
    Contract.Requires(e != null);
    E = e;
    Label = label;
    Attributes = attrs;
  }

  public void AddCustomizedErrorMessage(IToken tok, string s) {
    var args = new List<Expression>() { new StringLiteralExpr(tok, s, true) };
    IToken openBrace = tok;
    IToken closeBrace = new Token(tok.line, tok.col + 7 + s.Length + 1); // where 7 = length(":error ")
    this.Attributes = new UserSuppliedAttributes(tok, openBrace, closeBrace, args, this.Attributes);
  }
}

public class FrameExpression : IHasUsages {
  public readonly IToken tok;
  public readonly Expression E;  // may be a WildcardExpr
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(E != null);
    Contract.Invariant(!(E is WildcardExpr) || (FieldName == null && Field == null));
  }

  public readonly string FieldName;
  [FilledInDuringResolution] public Field Field;  // null if FieldName is

  /// <summary>
  /// If a "fieldName" is given, then "tok" denotes its source location.  Otherwise, "tok"
  /// denotes the source location of "e".
  /// </summary>
  public FrameExpression(IToken tok, Expression e, string fieldName) {
    Contract.Requires(tok != null);
    Contract.Requires(e != null);
    Contract.Requires(!(e is WildcardExpr) || fieldName == null);
    this.tok = tok;
    E = e;
    FieldName = fieldName;
  }

  public IToken NameToken => tok;
  public IEnumerable<INode> Children => new[] { E };
  public IEnumerable<IDeclarationOrUsage> GetResolvedDeclarations() {
    return new[] { Field }.Where(x => x != null);
  }
}

/// <summary>
/// This class represents a piece of concrete syntax in the parse tree.  During resolution,
/// it gets "replaced" by the expression in "ResolvedExpression".
/// </summary>
public abstract class ConcreteSyntaxExpression : Expression {
  [FilledInDuringResolution] public Expression ResolvedExpression;  // after resolution, manipulation of "this" should proceed as with manipulating "this.ResolvedExpression"
  public ConcreteSyntaxExpression(IToken tok)
    : base(tok) {
  }
  public override IEnumerable<INode> Children => ResolvedExpression == null ? Array.Empty<INode>() : new[] { ResolvedExpression };
  public override IEnumerable<Expression> SubExpressions {
    get {
      if (ResolvedExpression != null) {
        yield return ResolvedExpression;
      }
    }
  }

  public override IEnumerable<Type> ComponentTypes => ResolvedExpression.ComponentTypes;
}

/// <summary>
/// This class represents a piece of concrete syntax in the parse tree.  During resolution,
/// it gets "replaced" by the statement in "ResolvedStatement".
/// Adapted from ConcreteSyntaxStatement
/// </summary>
public abstract class ConcreteSyntaxStatement : Statement {
  [FilledInDuringResolution] public Statement ResolvedStatement;  // after resolution, manipulation of "this" should proceed as with manipulating "this.ResolvedExpression"
  public ConcreteSyntaxStatement(IToken tok, IToken endtok)
    : base(tok, endtok) {
  }
  public ConcreteSyntaxStatement(IToken tok, IToken endtok, Attributes attrs)
    : base(tok, endtok, attrs) {
  }
  public override IEnumerable<Statement> SubStatements {
    get {
      yield return ResolvedStatement;
    }
  }
}
public class ParensExpression : ConcreteSyntaxExpression {
  public readonly Expression E;
  public ParensExpression(IToken tok, Expression e)
    : base(tok) {
    E = e;
  }

  public override IEnumerable<Expression> SubExpressions {
    get {
      if (ResolvedExpression == null) {
        yield return E;
      } else {
        yield return ResolvedExpression;
      }
    }
  }
}

public class TypeExpr : ParensExpression {
  public readonly Type T;
  public TypeExpr(IToken tok, Expression e, Type t)
    : base(tok, e) {
    Contract.Requires(t != null);
    T = t;
  }

  public static Expression MaybeTypeExpr(Expression e, Type t) {
    if (t == null) {
      return e;
    } else {
      return new TypeExpr(e.tok, e, t);
    }
  }
}

public class DatatypeUpdateExpr : ConcreteSyntaxExpression, IHasUsages {
  public readonly Expression Root;
  public readonly List<Tuple<IToken, string, Expression>> Updates;
  [FilledInDuringResolution] public List<MemberDecl> Members;
  [FilledInDuringResolution] public List<DatatypeCtor> LegalSourceConstructors;
  [FilledInDuringResolution] public bool InCompiledContext;
  [FilledInDuringResolution] public Expression ResolvedCompiledExpression; // see comment for Resolver.ResolveDatatypeUpdate

  public DatatypeUpdateExpr(IToken tok, Expression root, List<Tuple<IToken, string, Expression>> updates)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(root != null);
    Contract.Requires(updates != null);
    Contract.Requires(updates.Count != 0);
    Root = root;
    Updates = updates;
  }

  public override IEnumerable<Expression> SubExpressions {
    get {
      if (ResolvedExpression == null) {
        yield return Root;
        foreach (var update in Updates) {
          yield return update.Item3;
        }
      } else {
        foreach (var e in base.SubExpressions) {
          yield return e;
        }
      }
    }
  }

  public IEnumerable<IDeclarationOrUsage> GetResolvedDeclarations() {
    return LegalSourceConstructors;
  }

  public IToken NameToken => tok;
}

/// <summary>
/// An AutoGeneratedExpression is simply a wrapper around an expression.  This expression tells the generation of hover text (in the Dafny IDE)
/// that the expression was no supplied directly in the program text and should therefore be ignored.  In other places, an AutoGeneratedExpression
/// is just a parenthesized expression, which means that it works just the like expression .E that it contains.
/// (Ironically, AutoGeneratedExpression, which is like the antithesis of concrete syntax, inherits from ConcreteSyntaxExpression, which perhaps
/// should rather have been called SemanticsNeutralExpressionWrapper.)
/// </summary>
public class AutoGeneratedExpression : ParensExpression {
  public AutoGeneratedExpression(IToken tok, Expression e)
    : base(tok, e) {
    Contract.Requires(tok != null);
    Contract.Requires(e != null);
  }

  /// <summary>
  /// This maker method takes a resolved expression "e" and wraps a resolved AutoGeneratedExpression
  /// around it.
  /// </summary>
  public static AutoGeneratedExpression Create(Expression e) {
    Contract.Requires(e != null);
    var a = new AutoGeneratedExpression(e.tok, e);
    a.type = e.Type;
    a.ResolvedExpression = e;
    return a;
  }
}

/// <summary>
/// When an actual parameter is omitted for a formal with a default value, the positional resolved
/// version of the actual parameter will have a DefaultValueExpression value. This has three
/// advantages:
/// * It allows the entire module to be resolved before any substitutions take place.
/// * It gives a good place to check for default-value expressions that would give rise to an
///   infinite expansion.
/// * It preserves the pre-substitution form, which gives compilers a chance to avoid re-evaluation
///   of actual parameters used in other default-valued expressions.
///
/// Note. Since DefaultValueExpression is a wrapper around another expression and can in several
/// places be expanded according to its ResolvedExpression, it is convenient to make DefaultValueExpression
/// inherit from ConcreteSyntaxExpression. However, there are some places in the code where
/// one then needs to pay attention to DefaultValueExpression's. Such places would be more
/// conspicuous if DefaultValueExpression were not an Expression at all. At the time of this
/// writing, a change to a separate type has shown to be more hassle than the need for special
/// attention to DefaultValueExpression's in some places.
/// </summary>
public class DefaultValueExpression : ConcreteSyntaxExpression {
  public readonly Formal Formal;
  public readonly Expression Receiver;
  public readonly Dictionary<IVariable, Expression> SubstMap;
  public readonly Dictionary<TypeParameter, Type> TypeMap;

  public DefaultValueExpression(IToken tok, Formal formal,
    Expression/*?*/ receiver, Dictionary<IVariable, Expression> substMap, Dictionary<TypeParameter, Type> typeMap)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(formal != null);
    Contract.Requires(formal.DefaultValue != null);
    Contract.Requires(substMap != null);
    Contract.Requires(typeMap != null);
    Formal = formal;
    Receiver = receiver;
    SubstMap = substMap;
    TypeMap = typeMap;
    Type = Resolver.SubstType(formal.Type, typeMap);
  }

  public override RangeToken RangeToken => new RangeToken(tok, tok);
}

/// <summary>
/// A NegationExpression e represents the value -e and is syntactic shorthand
/// for 0-e (for integers) or 0.0-e (for reals).
/// </summary>
public class NegationExpression : ConcreteSyntaxExpression {
  public readonly Expression E;
  public NegationExpression(IToken tok, Expression e)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(e != null);
    E = e;
  }
  public override IEnumerable<Expression> SubExpressions {
    get {
      if (ResolvedExpression == null) {
        // the expression hasn't yet been turned into a resolved expression, so use .E as the subexpression
        yield return E;
      } else {
        foreach (var ee in base.SubExpressions) {
          yield return ee;
        }
      }
    }
  }
}

public class ChainingExpression : ConcreteSyntaxExpression {
  public readonly List<Expression> Operands;
  public readonly List<BinaryExpr.Opcode> Operators;
  public readonly List<IToken> OperatorLocs;
  public readonly List<Expression/*?*/> PrefixLimits;
  public readonly Expression E;
  public ChainingExpression(IToken tok, List<Expression> operands, List<BinaryExpr.Opcode> operators, List<IToken> operatorLocs, List<Expression/*?*/> prefixLimits)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(operands != null);
    Contract.Requires(operators != null);
    Contract.Requires(operatorLocs != null);
    Contract.Requires(prefixLimits != null);
    Contract.Requires(1 <= operators.Count);
    Contract.Requires(operands.Count == operators.Count + 1);
    Contract.Requires(operatorLocs.Count == operators.Count);
    Contract.Requires(prefixLimits.Count == operators.Count);
    // Additional preconditions apply, see Contract.Assume's below

    Operands = operands;
    Operators = operators;
    OperatorLocs = operatorLocs;
    PrefixLimits = prefixLimits;
    Expression desugaring;
    // Compute the desugaring
    if (operators[0] == BinaryExpr.Opcode.Disjoint) {
      Expression acc = operands[0];  // invariant:  "acc" is the union of all operands[j] where j <= i
      desugaring = new BinaryExpr(operatorLocs[0], operators[0], operands[0], operands[1]);
      for (int i = 0; i < operators.Count; i++) {
        Contract.Assume(operators[i] == BinaryExpr.Opcode.Disjoint);
        var opTok = operatorLocs[i];
        var e = new BinaryExpr(opTok, BinaryExpr.Opcode.Disjoint, acc, operands[i + 1]);
        desugaring = new BinaryExpr(opTok, BinaryExpr.Opcode.And, desugaring, e);
        acc = new BinaryExpr(opTok, BinaryExpr.Opcode.Add, acc, operands[i + 1]);
      }
    } else {
      desugaring = null;
      for (int i = 0; i < operators.Count; i++) {
        var opTok = operatorLocs[i];
        var op = operators[i];
        Contract.Assume(op != BinaryExpr.Opcode.Disjoint);
        var k = prefixLimits[i];
        Contract.Assume(k == null || op == BinaryExpr.Opcode.Eq || op == BinaryExpr.Opcode.Neq);
        var e0 = operands[i];
        var e1 = operands[i + 1];
        Expression e;
        if (k == null) {
          e = new BinaryExpr(opTok, op, e0, e1);
        } else {
          e = new TernaryExpr(opTok, op == BinaryExpr.Opcode.Eq ? TernaryExpr.Opcode.PrefixEqOp : TernaryExpr.Opcode.PrefixNeqOp, k, e0, e1);
        }
        desugaring = desugaring == null ? e : new BinaryExpr(opTok, BinaryExpr.Opcode.And, desugaring, e);
      }
    }
    E = desugaring;
  }
}

/// <summary>
/// The parsing and resolution/type checking of expressions of the forms
///   0. ident &lt; Types &gt;
///   1. Expr . ident &lt; Types &gt;
///   2. Expr ( Exprs )
///   3. Expr [ Exprs ]
///   4. Expr [ Expr .. Expr ]
/// is done as follows.  These forms are parsed into the following AST classes:
///   0. NameSegment
///   1. ExprDotName
///   2. ApplySuffix
///   3. SeqSelectExpr or MultiSelectExpr
///   4. SeqSelectExpr
///
/// The first three of these inherit from ConcreteSyntaxExpression.  The resolver will resolve
/// these into:
///   0. IdentifierExpr or MemberSelectExpr (with .Lhs set to ImplicitThisExpr or StaticReceiverExpr)
///   1. IdentifierExpr or MemberSelectExpr
///   2. FuncionCallExpr or ApplyExpr
///
/// The IdentifierExpr's that forms 0 and 1 can turn into sometimes denote the name of a module or
/// type.  The .Type field of the corresponding resolved expressions are then the special Type subclasses
/// ResolutionType_Module and ResolutionType_Type, respectively.  These will not be seen by the
/// verifier or compiler, since, in a well-formed program, the verifier and compiler will use the
/// .ResolvedExpr field of whatever form-1 expression contains these.
///
/// Notes:
///   * IdentifierExpr and FunctionCallExpr are resolved-only expressions (that is, they don't contain
///     all the syntactic components that were used to parse them).
///   * Rather than the current SeqSelectExpr/MultiSelectExpr split of forms 3 and 4, it would
///     seem more natural to refactor these into 3: IndexSuffixExpr and 4: RangeSuffixExpr.
/// </summary>
public abstract class SuffixExpr : ConcreteSyntaxExpression {
  public readonly Expression Lhs;
  public SuffixExpr(IToken tok, Expression lhs)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(lhs != null);
    Lhs = lhs;
  }

  public override IEnumerable<INode> Children => ResolvedExpression == null ? new[] { Lhs } : base.Children;
}

public class NameSegment : ConcreteSyntaxExpression {
  public readonly string Name;
  public readonly List<Type> OptTypeArguments;
  public NameSegment(IToken tok, string name, List<Type> optTypeArguments)
    : base(tok) {
    Contract.Requires(tok != null);
    Contract.Requires(name != null);
    Contract.Requires(optTypeArguments == null || optTypeArguments.Count > 0);
    Name = name;
    OptTypeArguments = optTypeArguments;
  }
}

/// <summary>
/// An ExprDotName desugars into either an IdentifierExpr (if the Lhs is a static name) or a MemberSelectExpr (if the Lhs is a computed expression).
/// </summary>
public class ExprDotName : SuffixExpr {
  public readonly string SuffixName;
  public readonly List<Type> OptTypeArguments;

  /// <summary>
  /// Because the resolved expression only points to the final resolved declaration,
  /// but not the declaration of the Lhs, we must also include the Lhs.
  /// </summary>
  public override IEnumerable<INode> Children => new[] { Lhs, ResolvedExpression };

  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(SuffixName != null);
  }

  public ExprDotName(IToken tok, Expression obj, string suffixName, List<Type> optTypeArguments)
    : base(tok, obj) {
    Contract.Requires(tok != null);
    Contract.Requires(obj != null);
    Contract.Requires(suffixName != null);
    this.SuffixName = suffixName;
    OptTypeArguments = optTypeArguments;
  }
}

/// <summary>
/// An ApplySuffix desugars into either an ApplyExpr or a FunctionCallExpr
/// </summary>
public class ApplySuffix : SuffixExpr {
  public readonly IToken/*?*/ AtTok;
  public readonly IToken CloseParen;
  public readonly ActualBindings Bindings;
  public List<Expression> Args => Bindings.Arguments;

  public override IEnumerable<INode> Children => new[] { Lhs }.Concat(Args ?? Enumerable.Empty<INode>());

  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(Args != null);
  }

  public ApplySuffix(IToken tok, IToken/*?*/ atLabel, Expression lhs, List<ActualBinding> args, IToken closeParen)
    : base(tok, lhs) {
    Contract.Requires(tok != null);
    Contract.Requires(lhs != null);
    Contract.Requires(cce.NonNullElements(args));
    AtTok = atLabel;
    CloseParen = closeParen;
    Bindings = new ActualBindings(args);
    if (closeParen != null) {
      FormatTokens = new[] { closeParen };
    }
  }

  /// <summary>
  /// Create an ApplySuffix expression using the most basic pieces: a target name and a list of expressions.
  /// </summary>
  /// <param name="tok">The location to associate with the new ApplySuffix expression.</param>
  /// <param name="name">The name of the target function or method.</param>
  /// <param name="args">The arguments to apply the function or method to.</param>
  /// <returns></returns>
  public static Expression MakeRawApplySuffix(IToken tok, string name, List<Expression> args) {
    var nameExpr = new NameSegment(tok, name, null);
    var argBindings = args.ConvertAll(arg => new ActualBinding(null, arg));
    return new ApplySuffix(tok, null, nameExpr, argBindings, tok);
  }
}
