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
    private List<RequirePartitionFromProc> partitions = new();
    private List<RequirePartitionFromImpl> partitionsImpls = new();
    private List<Implementation> impls = new();

    protected override IEnumerable<ProgramModification> GetModifications(Program p) {
      partitions = new List<RequirePartitionFromProc>();
      var result = new List<ProgramModification>();

      partitionsImpls = new List<RequirePartitionFromImpl>();

      // populate partitions
      p = VisitProgram(p);

      /*foreach (var partition in partitions) {
        if (partition.HasUserDefinedPreconditions())
          result.Add(new ProgramModification(p, ProcedureName ?? partition.procedure.Name));
      }*/

      Console.Error.WriteLine("\n======== Extraction of Modifications ==========\n");
      foreach (var partition in partitionsImpls) {
        Console.Error.WriteLine("Partition");
        partition.ExtractUserDefinedPreconditions();
        Console.Error.WriteLine("\t Has user defined preconditions?");
        if (partition.HasUserDefinedPreconditions()) {
          Console.Error.WriteLine("\t yes - generating modifications");
          result.AddRange(partition.GetProgramModifications(p, impls));
          //result.Add(new ProgramModification(p, ProcedureName ?? partition.impl.Proc.Name));
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

      /*if (node.Name.StartsWith("Impl$$"))
        partitions.Add(new RequirePartitionFromProc(node));
      */
      return base.VisitProcedure(node);
    }

    public override Implementation VisitImplementation(Implementation node) {
      Console.Error.WriteLine("visit impl: " + node.Name);
      if (node.Name.StartsWith("CheckWellformed$$")) {
        Console.Error.WriteLine("\tAdded to assume parsing list");
        partitionsImpls.Add(new RequirePartitionFromImpl(node));
      } else if (node.Name.StartsWith("Impl$$")) {
        Console.Error.WriteLine("\tAdded to impl injection list");
        impls.Add(node);
      }

      return node;
    }

    public override Program VisitProgram(Program node) {
      program = node;
      return base.VisitProgram(node);
    }


    private class RequirePartitionFromProc {
      public readonly Procedure procedure;

      internal RequirePartitionFromProc(Procedure proc) {
        procedure = proc;
      }

      internal bool HasUserDefinedPreconditions() {
        List<Requires> req = procedure.Requires;
        Console.Error.WriteLine("Procedure: " + procedure.Name);
        foreach (var r in req) {
          Console.Error.WriteLine("\tRequire: " + r.Comment + " - " + r.Condition);
        }
        return false;
      }

    }

    private class RequirePartitionFromImpl {
      public readonly Implementation impl;

      public Block AssumeBlock;

      public readonly List<AssumeCmd> requireAssumes;

      internal RequirePartitionFromImpl(Implementation imp) {
        impl = imp;
        requireAssumes = new();
      }

      internal void ExtractUserDefinedPreconditions() {
        // A hack to make sure the Implementation is for a real implementation, rather than
        // a function called in the specification
        String AddMethodComment = "AddMethodImpl:";
        bool HasSeenAddMethodComment = false;

        StmtList req = impl.StructuredStmts;
        List<Block> blocks = impl.Blocks;
        Console.Error.WriteLine("Impl: " + impl);
        foreach (var block in blocks) {
          Console.Error.WriteLine("\t" + block.Label);
          List<Cmd> cmds = block.cmds;
          foreach (var cmd in cmds) {
            // we have seen the necessary comment
            if (HasSeenAddMethodComment) {
              // Add Assumes
              if (cmd is AssumeCmd assume) {
                Console.Error.WriteLine("******* ADD ASSUME BELOW");
                requireAssumes.Add(assume);
              }

              // Havoc stops
              if (cmd is HavocCmd havoc) {
                // remove the capture state
                requireAssumes.RemoveAt(0);
                Console.Error.WriteLine("******* TERMINATING HAVOC BELOW");
                return;
              }
            }

            // search for the comment key to know its a real implementation
            if (cmd is CommentCmd commentLine) {
              if (commentLine.Comment.StartsWith(AddMethodComment)) {
                HasSeenAddMethodComment = true;
                AssumeBlock = block;
                Console.Error.WriteLine("******* FOUND COMMENT BELOW");
              }
            }
            Console.Error.Write("\t\t" + cmd);
          }
        }
      }


      // Program p will be modified
      // The implementation list allows us to insert into the original program's 
      // implementation rather than the well-formed
      internal List<ProgramModification> GetProgramModifications(Program p, List<Implementation> impls) {
        List<ProgramModification> mods = new();

        Console.Error.WriteLine("Get Program Modifications: ");
        // for each assume, generate positive and negative assert modification
        foreach (AssumeCmd assume in requireAssumes) {
          Console.Error.WriteLine("Statement:" + assume.ToString());
          String text = assume.ToString().Replace("assume ", "assert (").Replace(";\n", ") == false;\n");
          Console.Error.WriteLine("Replaced With:" + text);

          //positive
          Cmd positiveCommand = GetCmd(text);
          Console.Error.WriteLine("Command: " + positiveCommand);

          // negative
          // TODO: 

          String methodName = impl.Name.Replace("CheckWellformed$$", "");
          // find matching impl
          Implementation i = impls.Find(x => x.Name.EndsWith(methodName));
          if (i == null) {
            Console.Error.WriteLine("******************UNEXPECTED TERMINATION*************************");
            Console.Error.WriteLine("******************UNEXPECTED TERMINATION*************************");
            Console.Error.WriteLine("******************UNEXPECTED TERMINATION*************************");
            Console.Error.WriteLine("******************UNEXPECTED TERMINATION*************************");
          } else {
            // Add to the implementation
            Block firstBlock = i.Blocks[0];
            Console.Error.WriteLine("Injected into the Implementation hopefully");
            firstBlock.cmds.Insert(0, positiveCommand);
            mods.Add(new ProgramModification(p, impl.Name));
            firstBlock.cmds.RemoveAt(0);

            // Also lets add to the checkwellformed
            AssumeBlock.cmds.Insert(0, positiveCommand);
            mods.Add(new ProgramModification(p, impl.Name));
            AssumeBlock.cmds.RemoveAt(0);
          }


          //AssumeBlock.cmds.Insert(0, positiveCommand);
          //mods.Add(new ProgramModification(p, impl.Name));
          //AssumeBlock.cmds.RemoveAt(0);



        }
        return mods;
      }

      internal bool HasUserDefinedPreconditions() {
        return requireAssumes.Count > 0;
      }

    }
  }
}