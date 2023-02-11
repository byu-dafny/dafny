using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Boogie;

namespace DafnyTestGeneration {
  public class PartitionModification : ProgramModification {

    private readonly int blockId;
    private static readonly ISet<int> covered = new HashSet<int>();

    public PartitionModification(Program program, string procedure,
      int blockId) : base(program, procedure) {
      this.blockId = blockId;
    }

    public override async Task<string?> GetCounterExampleLog() {

      if (covered.Contains(blockId)) {
        return null;
      }
      var log = await base.GetCounterExampleLog();
      if (log == null) {
        return null;
      }
      
      return log;
    }
  }
}