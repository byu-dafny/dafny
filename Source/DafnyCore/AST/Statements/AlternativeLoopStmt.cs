using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;

namespace Microsoft.Dafny;

public class AlternativeLoopStmt : LoopStmt, ICloneable<AlternativeLoopStmt> {
  public readonly bool UsesOptionalBraces;
  public readonly List<GuardedAlternative> Alternatives;
  [ContractInvariantMethod]
  void ObjectInvariant() {
    Contract.Invariant(Alternatives != null);
  }

  public AlternativeLoopStmt Clone(Cloner cloner) {
    return new AlternativeLoopStmt(cloner, this);
  }

  public AlternativeLoopStmt(Cloner cloner, AlternativeLoopStmt original) : base(cloner, original) {
    Alternatives = original.Alternatives.ConvertAll(cloner.CloneGuardedAlternative);
    UsesOptionalBraces = original.UsesOptionalBraces;
  }

  public AlternativeLoopStmt(IToken tok, IToken endTok,
    List<AttributedExpression> invariants, Specification<Expression> decreases, Specification<FrameExpression> mod,
    List<GuardedAlternative> alternatives, bool usesOptionalBraces)
    : base(tok, endTok, invariants, decreases, mod) {
    Contract.Requires(tok != null);
    Contract.Requires(endTok != null);
    Contract.Requires(alternatives != null);
    this.Alternatives = alternatives;
    this.UsesOptionalBraces = usesOptionalBraces;
  }
  public AlternativeLoopStmt(IToken tok, IToken endTok,
    List<AttributedExpression> invariants, Specification<Expression> decreases, Specification<FrameExpression> mod,
    List<GuardedAlternative> alternatives, bool usesOptionalBraces, Attributes attrs)
    : base(tok, endTok, invariants, decreases, mod, attrs) {
    Contract.Requires(tok != null);
    Contract.Requires(endTok != null);
    Contract.Requires(alternatives != null);
    this.Alternatives = alternatives;
    this.UsesOptionalBraces = usesOptionalBraces;
  }
  public override IEnumerable<Statement> SubStatements {
    get {
      foreach (var alt in Alternatives) {
        foreach (var s in alt.Body) {
          yield return s;
        }
      }
    }
  }

  public override IEnumerable<Expression> SpecificationSubExpressions {
    get {
      foreach (var e in base.SpecificationSubExpressions) { yield return e; }
      foreach (var alt in Alternatives) {
        foreach (var e in Attributes.SubExpressions(alt.Attributes)) {
          yield return e;
        }
      }
    }
  }

  public override IEnumerable<Expression> NonSpecificationSubExpressions {
    get {
      foreach (var e in base.NonSpecificationSubExpressions) { yield return e; }
      foreach (var alt in Alternatives) {
        yield return alt.Guard;
      }
    }
  }

  public override IEnumerable<INode> Children => SpecificationSubExpressions.Concat<INode>(Alternatives);
}