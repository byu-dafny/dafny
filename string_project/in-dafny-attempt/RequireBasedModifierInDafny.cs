using System.Collections.Generic;
using Microsoft.Boogie;
using System;
using System.Linq;


namespace DafnyTestGeneration {

  /// <summary>
  /// A version of ProgramModifier that inserts assertions into the code
  /// that fail when a particular basic block is visited
  /// </summary>
  public class RequireBasedModifierInDafny : ProgramModifier {
    private Program? program; // the original program
    private List<RequirePartitionFromProc> partitions = new();
    private List<RequirePartitionFromImpl> partitionsImpls = new();
    private List<Implementation> impls = new();



    private Program? actualBoogiePrograms;
    private List<ProgramModification> results = new();


    public RequireBasedModifierInDafny(Microsoft.Dafny.Program program) {
      // Admitted hack
      // 1. Ingest the Dafny program,
      // 2. Modify the Dafny program
      // 3. Translate to boogie so we can use this one there instead of the passed in one
      // 4. So we can call GetModifications()
      // 5. And return the precomputed modifications

      DafnyVisitor v = new DafnyVisitor();
      Console.Error.WriteLine("Constructor START");
      foreach (var module in program.Modules()) {
        foreach (var decl in module.TopLevelDecls) {
          Console.Error.WriteLine("Module Decl" + decl + "||" + decl.WhatKind);
          if (decl is Microsoft.Dafny.ClassDecl c) {
            Console.Error.WriteLine("   Class " + c);
            foreach (var member in c.Members) {
              Console.Error.WriteLine("       MEMBERS " + member);
              if (member is Microsoft.Dafny.Method m) {
                Console.Error.WriteLine("               Method" + m);
                v.Visit(m, true);
              }
            }
          }
        }
      }


      // Translate the Program to Boogie:
      var oldPrintInstrumented = Microsoft.Dafny.DafnyOptions.O.PrintInstrumented;
      Microsoft.Dafny.DafnyOptions.O.PrintInstrumented = true;
      var actualBoogiePrograms = Microsoft.Dafny.Translator
        .Translate(program, program.reporter)
        .ToList().ConvertAll(tuple => tuple.Item2);
      Microsoft.Dafny.DafnyOptions.O.PrintInstrumented = oldPrintInstrumented;
    }





    protected override IEnumerable<ProgramModification> GetModifications(Program p) {
      return results;
    }



    public override Program VisitProgram(Program node) {
      program = node;
      return base.VisitProgram(node);
    }

    internal class DafnyVisitor : Microsoft.Dafny.TopDownVisitor<bool> {
      public new void Visit(Microsoft.Dafny.Method method, bool st) {
        Console.Error.WriteLine("DAFNY VISITOR:" + method.Ens);
        Visit(method.Ens, st);
        Visit(method.Req, st);
        Visit(method.Mod.Expressions, st);
        Visit(method.Decreases.Expressions, st);
        if (method.Body != null) {
          // WHAT THE FLIP DO I PUT HERE....
          // TODO: FIX this
          method.Body.Body.Prepend(new Microsoft.Dafny.AssertStmt());
          Visit(method.Body, st);
        }
        //TODO More?
      }
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