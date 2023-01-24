using System;
using System.Collections.Generic;
using Microsoft.Boogie;

namespace DafnyTestGeneration {

  /// <summary>
  /// A version of ProgramModifier that inserts assertions into the code
  /// that fail when a particular basic block is visited
  /// </summary>
  public class InputMapper : ReadOnlyVisitor {
    
    private IDictionary<Procedure, List<Requires>> requires;
    private Program? program; // the original program

    public InputMapper(IEnumerable<Program> programs) {
      program = MergeBoogiePrograms(programs);
      requires = new Dictionary<Procedure, List<Requires>>();
    }

    public IDictionary<Procedure, List<Requires>> GetImplementationsAndRequires() {
      VisitProgram(program);
      return requires;
    }
    
    /// <summary>
    /// Merge Boogie Programs by removing any duplicate top level declarations
    /// (these typically come from DafnyPrelude.bpl)
    /// </summary>
    private static Program MergeBoogiePrograms(IEnumerable<Program> programs) {
      // Merge all programs into one first:
      var program = new Program();
      foreach (var p in programs) {
        program.AddTopLevelDeclarations(p.TopLevelDeclarations);
      }
      // Remove duplicates afterwards:
      var declarations = new Dictionary<string, HashSet<string?>>();
      var toRemove = new List<Declaration>();
      foreach (var declaration in program.TopLevelDeclarations) {
        var typeName = declaration.GetType().Name;
        if (typeName.Equals("Axiom")) {
          continue;
        }
        if (!declarations.ContainsKey(typeName)) {
          declarations[typeName] = new();
        }
        if (declarations[typeName].Contains(declaration.ToString())) {
          toRemove.Add(declaration);
        } else {
          declarations[typeName].Add(declaration.ToString());
        }
      }
      toRemove.ForEach(x => program.RemoveTopLevelDeclaration(x));
      return program;
    }

    public override Procedure VisitProcedure(Procedure node) {
      if (node != null && node.Name.Contains("Call$$M.")) {
        requires.Add(new KeyValuePair<Procedure,List<Requires>>(node, node.Requires));
      }
      return node;
    }
  }
}