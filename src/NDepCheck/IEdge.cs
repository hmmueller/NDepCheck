using JetBrains.Annotations;

namespace NDepCheck {
    public interface IWithCt {
        int Ct { get; }
        int NotOkCt { get; }
    }

    public interface IEdge : IWithCt {
        [NotNull]
        INode UsingNode { get; }
        [NotNull]
        INode UsedNode { get; }
        bool Hidden { get; set; }
        [NotNull]
        string GetDotRepresentation(int? stringLengthForIllegalEdges);
        void MarkOnCycle();
        void MarkCarrysTransitive();
        [NotNull]
        string AsStringWithTypes(bool withNotOkExampleInfo);
    }
}