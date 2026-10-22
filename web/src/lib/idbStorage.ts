// Armazenamento chave → texto no IndexedDB, no formato que o persister do TanStack Query espera.
// Sem IndexedDB (modo privado antigo etc.) tudo vira no-op: o app só deixa de funcionar offline.

const DB_NAME = 'arco'
const STORE = 'kv'

let dbPromise: Promise<IDBDatabase> | null = null

function openDb(): Promise<IDBDatabase> {
  dbPromise ??= new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, 1)
    req.onupgradeneeded = () => req.result.createObjectStore(STORE)
    req.onsuccess = () => resolve(req.result)
    req.onerror = () => reject(req.error)
  })
  return dbPromise
}

async function run<T>(mode: IDBTransactionMode, action: (store: IDBObjectStore) => IDBRequest<T>): Promise<T> {
  const db = await openDb()
  return new Promise((resolve, reject) => {
    const req = action(db.transaction(STORE, mode).objectStore(STORE))
    req.onsuccess = () => resolve(req.result)
    req.onerror = () => reject(req.error)
  })
}

export const idbStorage = {
  getItem: async (key: string): Promise<string | null> => {
    try {
      return ((await run('readonly', (s) => s.get(key))) as string | undefined) ?? null
    } catch {
      return null
    }
  },
  setItem: async (key: string, value: string): Promise<void> => {
    try {
      await run('readwrite', (s) => s.put(value, key))
    } catch {
      // sem armazenamento local
    }
  },
  removeItem: async (key: string): Promise<void> => {
    try {
      await run('readwrite', (s) => s.delete(key))
    } catch {
      // sem armazenamento local
    }
  },
}
