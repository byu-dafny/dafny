using System.Collections.Generic;
using Microsoft.Boogie;

namespace DafnyTestGeneration {

  /// <summary>
  /// A version of ProgramModifier that inserts assertions into the code
  /// that fail when a particular basic block is visited
  /// </summary>
  public class PartitionModifier : ProgramModifier {

    private Implementation? implementation; // the implementation currently traversed
    private Program? program; // the original program
    private List<ProgramModification> modifications = new();

    protected override IEnumerable<ProgramModification> GetModifications(Program p) {
      modifications = new List<ProgramModification>();
      VisitProgram(p);
      return modifications;
    }

    public override Implementation VisitImplementation(Implementation node) {
      implementation = node;
      if (ImplementationIsToBeTested(node)) {
        if (program == null || implementation == null) {
          return node;
        }
        // node.
        node.Blocks[0].cmds.Insert(2, GetCmd("assert false;"));
        // node.Blocks[0].cmds.Add(GetCmd("assert false;"));
        var record = new PartitionModification(program,
          ImplementationToTarget?.VerboseName ?? implementation.VerboseName,
          node.UniqueId);
        modifications.Add(record);
        // node.Blocks[0].cmds.RemoveAt(node.Blocks[0].cmds.Count - 1);
        node.Blocks[0].cmds.RemoveAt(2);
        
        // add code to loop through the preconditions and do this for each precondition...
      }
      return node;
    }

    public override Program VisitProgram(Program node) {
      program = node;
      return base.VisitProgram(node);
    }
  }
}