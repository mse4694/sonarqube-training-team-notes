import { beforeEach, describe, expect, it } from 'vitest';
import { GET as list, POST as create } from '@/app/api/notes/route';
import { DELETE as del, GET as getNote, PATCH as patch } from '@/app/api/notes/[id]/route';
import { GET as search } from '@/app/api/notes/search/route';
import { GET as files } from '@/app/api/files/route';
import { GET as adminUsers } from '@/app/api/admin/users/route';
import { freshDatabase } from '../helpers/testdb';
import { ctx, loginAs, req } from '../helpers/http';

beforeEach(freshDatabase);

describe('notes (happy path)', () => {
  it('lists only my team notes', async () => {
    const cookie = await loginAs('a');
    const { notes } = await (await list(req('/api/notes', { cookie }))).json();
    expect(notes.map((n: { id: number }) => n.id)).toEqual([1, 2, 5]);
  });

  it('creates a note in my team', async () => {
    const cookie = await loginAs('b');
    const res = await create(req('/api/notes', { cookie, method: 'POST', json: { title: 'Beta retro' } }));
    expect(res.status).toBe(201);
    expect((await res.json()).note.team_id).toBe(2);
  });

  it('searches my team notes', async () => {
    const cookie = await loginAs('a');
    const { results } = await (await search(req('/api/notes/search?q=sprint', { cookie }))).json();
    expect(results).toEqual([{ id: 1, title: 'Sprint 12 plan', team_id: 1 }]);
  });

  it('reads, edits and deletes my own note', async () => {
    const cookie = await loginAs('a');
    expect((await getNote(req('/api/notes/1', { cookie }), ctx(1))).status).toBe(200);
    const edited = await patch(req('/api/notes/1', { cookie, method: 'PATCH', json: { title: 'Sprint 12 (final)' } }), ctx(1));
    expect(edited.status).toBe(200);
    const { note } = await (await getNote(req('/api/notes/1', { cookie }), ctx(1))).json();
    expect(note.title).toBe('Sprint 12 (final)');
    expect((await del(req('/api/notes/1', { cookie, method: 'DELETE' }), ctx(1))).status).toBe(204);
  });

  it('downloads an attachment', async () => {
    const cookie = await loginAs('a');
    const res = await files(req('/api/files?name=release-checklist.txt', { cookie }));
    expect(res.status).toBe(200);
    expect(await res.text()).toContain('Release checklist');
  });

  it('admin can list users', async () => {
    const cookie = await loginAs('admin');
    const res = await adminUsers(req('/api/admin/users', { cookie }));
    expect(res.status).toBe(200);
    expect((await res.json()).users).toHaveLength(3);
  });
});
