using System.Collections.Generic;
using Program = Microsoft.Boogie.Program;
using Microsoft.Boogie;
using Microsoft.Dafny;
using System.Reflection;
using System;

namespace DafnyTestGeneration {

  public class MCDCBasedModifier : ProgramModifier {

    protected override IEnumerable<ProgramModification> GetModifications(Program p) {
      var result = new List<ProgramModification>();
      
      DecisionVisitor visitor = new DecisionVisitor();
      visitor.VisitProgram(p);  

      MCDCSuiteGenerator generator = new MCDCSuiteGenerator();
      var allTestSets = generator.GetTestSuite(visitor.GotoDecisionMapper);
      var mapper = generator.SymbolStrToExpr; 

      AssertionInjectionVisitor injectionVisitor = new AssertionInjectionVisitor(allTestSets, mapper);


      return result;
    }

    public class AssertionInjectionVisitor : StandardVisitor {

      private Dictionary<Expr, List<Dictionary<String, bool>>?> allTestSets;
      private Dictionary<String, String> symbolStrToExprStr;  //TODO there's no way to map symbol to test suite, unless you save Expr in symbolStrToExprStr
                                                              // but that would require visiting each expression in the read only visitor, instead you could 
                                                              // save the allTestSets key as a string??

      public AssertionInjectionVisitor(Dictionary<Expr, List<Dictionary<String, bool>>> allTestSets, 
          Dictionary<String, String> symbolStrToExprStr) {
        this.allTestSets = allTestSets;
        this.symbolStrToExprStr = symbolStrToExprStr;
      }

    }

    
  }
}