using MoscoRun.Domain.Analysis;

namespace MoscoRun.Infrastructure.Persistence;

public sealed record ScientificImportedRevision(string Revision, long? RevisionOrder, ScientificObservation Observation, ScientificImportProvenance? Provenance);
