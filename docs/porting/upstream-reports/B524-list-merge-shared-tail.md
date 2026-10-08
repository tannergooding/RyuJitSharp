# `jitstd::list::merge` leaves source tail nodes in both lists

**Status:** reported in [dotnet/runtime#135435](https://github.com/dotnet/runtime/issues/135435).
The implementation was verified at pinned revision
`33baf8ee337b20dd0f184b69a6f09be92850bf9e` and current upstream commit
`7fd4be4b29175b1007cb5a1bd701f057b2fe08be`.

## Defect

`list<T, Allocator>::merge` copies source elements into the destination while
the comparator selects them. If source nodes remain after that loop, it links
the remaining source chain onto the destination and assigns the source tail to
the destination tail, but does not update the source list's head, tail, or
size. Both lists therefore retain links to the same nodes.

The native `list` destructor calls `destroy_helper`, which walks backward from
each list's tail and destroys/deallocates the nodes. Clearing or destroying
either list can therefore leave the other list linked to already-destroyed
nodes; later traversal or destruction can access or destroy those nodes again.
Not using the source list after `merge` does not avoid its destructor.

For example, with the default comparator, merging destination values
`[1, 4, 6]` and source values `[2, 3, 5, 7]` produces destination values
`[1, 2, 3, 4, 5, 6, 7]` while the source still reports `[2, 3, 5, 7]`; the
destination and source share the final node. The pinned JIT tree contains no
`list.merge` call sites, so this is a source-confirmed generic-container defect,
not a demonstrated JIT execution failure.

## Port handling

`JitStdList.merge` mirrors the pinned insertion and tail-link algorithm,
including the shared tail, and its regression test verifies value order,
source metadata, shared tail identity, and forward iterator termination. The
test does not claim safe destruction of both lists after aliasing. Keep the
managed behavior aligned until the upstream correction is accepted, then apply
that correction to both implementations.
