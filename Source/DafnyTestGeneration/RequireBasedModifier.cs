using System.Collections.Generic;
using Microsoft.Boogie;
using System;

namespace DafnyTestGeneration {

  /// <summary>
  /// A version of ProgramModifier that inserts assertions into the code
  /// that fail when a particular basic block is visited
  /// </summary>
  public class RequireBasedModifier : ProgramModifier {
    private Program? program; // the original program
    private List<InputPartition> partitions = new();
    private List<Procedure> procsWithCall = new();
    private List<Procedure> procs = new();
    private List<Implementation> impls = new();

    protected override IEnumerable<ProgramModification> GetModifications(Program p) {
      var result = new List<ProgramModification>();

      // populate partitions
      p = VisitProgram(p);

      Console.Error.WriteLine("\n======== Extraction of Modifications ==========\n");
      foreach (var partition in partitions) {
        Console.Error.WriteLine("Partition");
        Console.Error.WriteLine("\t Has user defined preconditions?");
        if (partition.HasUserDefinedPreconditions()) {
          Console.Error.WriteLine("\t yes - generating modifications");
          result.AddRange(partition.GetProgramModifications(p, impls));
        }
      }

      Console.Error.WriteLine("Summary:");
      Console.Error.WriteLine("# Of Modifications generated: " + result.Count);
      return result;
    }

    //TODO: I added this. We need to parse requires and ensures blocks in here
    // and add then like the path based modifier to class state, then 
    // after visit is complete, generate modifications based on those.
    public override Procedure VisitProcedure(Procedure node) {
      if (program == null) {
        return base.VisitProcedure(node);
      }

      if (node.Name.StartsWith("Impl$$")) {
        partitions.Add(new InputPartition(node, procsWithCall));
        procs.Add(node);
      }
      if (node.Name.StartsWith("Call$$")) {
        procsWithCall.Add(node);
      }
      return base.VisitProcedure(node);
    }

    public override Implementation VisitImplementation(Implementation node) {
      Console.Error.WriteLine("visit impl: " + node.Name);
      if (node.Name.StartsWith("Impl$$")) {
        impls.Add(node);
      }

      return node;
    }

    public override Program VisitProgram(Program node) {
      program = node;
      return base.VisitProgram(node);
    }


    private class InputPartition {
      public readonly Procedure procedure;

      public readonly List<Procedure> proceduresWithCall;
      public List<Requires> requires;
      internal InputPartition(Procedure proc, List<Procedure> withCalls) {
        procedure = proc;
        this.proceduresWithCall = withCalls;
        this.requires = new();


        Console.Error.WriteLine("Found Procedure: " + procedure.Name);
        bool foundComment = false;
        foreach (var r in procedure.Requires) {
          Console.Error.WriteLine("\tWith Require: " + r.Comment + " - " + r.Condition);
          if (!foundComment &&
              r.Comment != null &&
              r.Comment.StartsWith("user-defined preconditions")) {
            foundComment = true;
          }
          if (foundComment) {
            Console.Error.WriteLine("\t\tkept require ^^");
            this.requires.Add(r);
          }
        }
      }

      internal bool HasUserDefinedPreconditions() {
        return this.requires.Count > 0;
      }

      internal List<ProgramModification> GetProgramModifications(Program p, List<Implementation> impls) {
        String matchingPartialName = procedure.Name.Substring(4);

        // strip out requires statements everywhere.
        Procedure call = this.proceduresWithCall.Find(x => x.Name.EndsWith(matchingPartialName));
        Console.Error.WriteLine("Found matching call" + call);
        /*foreach (var r in this.requires) {
          procedure.Requires.Remove(r); // remove the user defined requires (except we do want to only comment out one at a time later)
          // also need to remove it from the `procedure Call$$` requires as well?
          if (call != null) {
            Requires matchingRequires = call.Requires.Find(x => x.Condition.ToString().Equals(r.Condition.ToString()));
            call.Requires.Remove(matchingRequires);
          }
        }*/

        List<ProgramModification> mods = new();
        Implementation matchingImpl = impls.Find(x => x.Name.EndsWith(matchingPartialName));
        Requires firstRequires = requires[0];
        if (matchingImpl != null) {
          Console.Error.WriteLine("Input Partition with matching Partial Name (" + matchingPartialName + ") = " + matchingImpl.Name);
          // inject the assert statements
          var firstBlock = matchingImpl.Blocks[0];
          if (firstBlock != null) {


            // new stuff for each requires
            foreach (Requires aRequire in requires) {
              // strip out require statements, to be added back in later
              procedure.Requires.Remove(aRequire);
              Requires matchingRequires = null;
              if (call != null) {
                matchingRequires = call.Requires.Find(x => x.Condition.ToString().Equals(aRequire.Condition.ToString()));
                call.Requires.Remove(matchingRequires);
              }




              String negatedRequires = "assert (" + aRequire.Condition.ToString() + ") == Lit(false);";
              String falseAssertion = "assert false;";
              Console.Error.WriteLine("Injecting:" + negatedRequires);
              Console.Error.WriteLine("Injecting:" + falseAssertion);

              //positive
              Cmd assumeTrueFirst = GetAssumeCmd(new List<string>());
              Cmd negatedRequiresCmd = GetCmd(negatedRequires);
              Cmd assumeTrueSecond = GetAssumeCmd(new List<string>());
              Cmd falseAssertionCmd = GetCmd(falseAssertion);
              Console.Error.WriteLine("Command: " + negatedRequiresCmd);
              Console.Error.WriteLine("Command: " + falseAssertionCmd);

              firstBlock.cmds.Insert(6, assumeTrueFirst);
              firstBlock.cmds.Insert(7, negatedRequiresCmd);
              firstBlock.cmds.Insert(8, assumeTrueSecond);
              firstBlock.cmds.Insert(9, falseAssertionCmd);
              mods.Add(new ProgramModification(p, procedure.Name));
              firstBlock.cmds.RemoveAt(9);
              firstBlock.cmds.RemoveAt(8);
              firstBlock.cmds.RemoveAt(7);
              firstBlock.cmds.RemoveAt(6);


              // add the stripped out require statements
              procedure.Requires.Add(aRequire);
              if (call != null && matchingRequires != null) {
                call.Requires.Add(matchingRequires);
              }

            }
          }
        }

        return mods;
      }

    }
  }
}