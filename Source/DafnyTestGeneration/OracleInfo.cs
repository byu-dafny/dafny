using System.Collections.Generic;
using System.Linq;
using Microsoft.Dafny;
using Token = Microsoft.Boogie.Token;
using static System.ObjectExtensions;

namespace DafnyTestGeneration {

  /// <summary> Extract oracle-related info from a parsed Dafny program </summary>
  public class OracleInfo {

    // method -> (list of argument names)
    public readonly Dictionary<string, List<string>> argNames;
    //method -> (list of return variable names)
    public readonly Dictionary<string, List<string>> retNames;

    // method -> (list of ensures)
    public readonly Dictionary<string, List<string>> ensList;

    public Dictionary<string, List<AttributedExpression>> ensObjects;
    public readonly List<string> tempData;
    public OracleInfo(Microsoft.Dafny.Program program) {
      argNames = new Dictionary<string, List<string>>();
      retNames = new Dictionary<string, List<string>>();
      ensList = new Dictionary<string, List<string>>();
      ensObjects = new Dictionary<string, List<AttributedExpression>>();
      tempData = new List<string>();
      var visitor = new OracleInfoExtractor(this);
      visitor.Visit(program);
    }

    public IList<string> GetArgNames(string method) {
      return argNames[method];
    }    
    public IList<string> GetRetNames(string method) {
      return retNames[method];
    }

    public IList<string> GetEnsList(string method) {
      return ensList[method];
    }

    /// <summary>
    /// Fills in the Oracle Info data by traversing the AST
    /// </summary>
    private class OracleInfoExtractor : BottomUpVisitor {

      private readonly OracleInfo info;

      // path to a method in the tree of modules and classes:
      private readonly List<string> path;

      internal OracleInfoExtractor(OracleInfo info) {
        this.info = info;
        path = new List<string>();
      }

      internal void Visit(Microsoft.Dafny.Program p) {
        Visit(p.DefaultModule);
      }

      private void Visit(TopLevelDecl d) {
        if (d is LiteralModuleDecl moduleDecl) {
          Visit(moduleDecl);
        } else if (d is ClassDecl classDecl) {
          Visit(classDecl);
        }
      }

      private void Visit(LiteralModuleDecl d) {
        if (d.Name.Equals("_module")) {
          d.ModuleDef.TopLevelDecls.ForEach(Visit);
          return;
        }
        path.Add(d.Name);
        d.ModuleDef.TopLevelDecls.ForEach(Visit);
        path.RemoveAt(path.Count - 1);
      }

      private void Visit(ClassDecl d) {
        if (d.Name == "_default") {
          d.Members.ForEach(Visit);
          return;
        }
        path.Add(d.Name);
        d.Members.ForEach(Visit);
        path.RemoveAt(path.Count - 1);
      }

      private void Visit(MemberDecl d) {
        if (d is Method method) {
          Visit(method);
        }
      }

      private new void Visit(Method m) {
        var methodName = m.Name;
        if (path.Count != 0) {
          methodName = $"{string.Join(".", path)}.{methodName}";
        }
        var argNames = m.Ins.Select(arg => arg.Name.ToString()).ToList();
        var retNames = m.Outs.Select(ret => ret.Name.ToString()).ToList();
        var ensList = m.Ens.Select(ens => Printer.ExprToString(ens.E)).ToList();
        var ensObjects = m.Ens;
        info.argNames[methodName] = argNames;
        info.retNames[methodName] = retNames;
        info.ensList[methodName] = ensList;
        info.ensObjects[methodName] = ensObjects;
      }
    }

  }
    public class OracleEnsReplacer<State> : TopDownVisitor<State> {
      /// <summary> Visitor that replaces variables in an ensures using a given map</summary>
        public List<AttributedExpression> ens;
        private Dictionary<string, string> varMap;

        private Dictionary<string, string> oldVarMap;

        private bool inOldExpr;
        public OracleEnsReplacer(List<AttributedExpression> ens, Dictionary<string, string> varMap, Dictionary<string, string> oldVarMap, State st) {
            this.ens = System.ObjectExtensions.Copy(ens);
            this.varMap = varMap;
            this.oldVarMap = oldVarMap;
            this.inOldExpr = false;
            Visit(this.ens, st);
        }
        override protected bool VisitOneExpr(Expression expr, ref State st) {
            if (expr is Microsoft.Dafny.NameSegment) {
                NameSegment nameSegment = (NameSegment)expr;
                if (this.inOldExpr){
                    if (this.oldVarMap.ContainsKey(nameSegment.Name)){
                        nameSegment.Name = this.oldVarMap[nameSegment.Name];
                    }
                }
                else{
                    if (this.varMap.ContainsKey(nameSegment.Name)){
                        nameSegment.Name = this.varMap[nameSegment.Name];
                    }
                }
            }
            return true;
        }

        public override void Visit(Expression expr, State st) {
            //Contract.Requires(expr != null);
            if (expr.GetType().ToString() == "Microsoft.Dafny.OldExpr"){
                this.inOldExpr = true;
            }
            if (VisitOneExpr(expr, ref st)) {
                // recursively visit all subexpressions and all substatements
                foreach (Expression subExpr in expr.SubExpressions)
                {
                    Visit(subExpr, st);
                }
                if (expr is StmtExpr) {
                    // a StmtExpr also has a sub-statement
                    var e = (StmtExpr)expr;
                    Visit(e.S, st);
                }
            }
            if (expr.GetType().ToString() == "Microsoft.Dafny.OldExpr"){
                this.inOldExpr = false;
            }
        }
    }
}