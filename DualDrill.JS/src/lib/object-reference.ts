type UUID = ReturnType<typeof crypto.randomUUID>;
const ObjectReferences = new Map<UUID, unknown>();

export function createReference(obj: unknown) {
  const id = crypto.randomUUID();
  ObjectReferences.set(id, obj);
  return id;
}

export function getReference(id: UUID) {
  return ObjectReferences.get(id);
}

export function disposeReference(id: UUID) {
  ObjectReferences.delete(id);
}
