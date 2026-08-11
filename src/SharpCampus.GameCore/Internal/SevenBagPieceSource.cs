namespace SharpCampus.GameCore.Internal;

// The standard 7-bag "Random Generator": deal one shuffled bag of all seven kinds, refill when
// empty, so no kind can drought for more than 12 deals. Source: https://harddrop.com/wiki/Random_Generator
internal sealed class SevenBagPieceSource : IPieceSource
{
    private static readonly PieceKind[] _allKinds =
        [PieceKind.I, PieceKind.O, PieceKind.T, PieceKind.S, PieceKind.Z, PieceKind.J, PieceKind.L];

    private readonly PieceKind[] _bag = new PieceKind[Tetrominoes.KindCount];
    private SplitMix64 _random;
    private int _index = Tetrominoes.KindCount;

    public SevenBagPieceSource(ulong seed) => _random = new SplitMix64(seed);

    public PieceKind Next()
    {
        if (_index >= _bag.Length)
        {
            Refill();
        }

        return _bag[_index++];
    }

    private void Refill()
    {
        Array.Copy(_allKinds, _bag, _bag.Length);
        for (var i = _bag.Length - 1; i > 0; i--)
        {
            var j = _random.NextInt(i + 1);
            (_bag[i], _bag[j]) = (_bag[j], _bag[i]);
        }

        _index = 0;
    }
}
