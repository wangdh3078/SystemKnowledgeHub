using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SystemKnowledgeHub.Api.Persistence;
namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class HumanConfirmationLifecycleMigrationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Fresh_and_upgrade_preserve_history_and_enforce_lifecycle_constraints(bool upgrade)
    {
        await using var c=new SqliteConnection("Data Source=:memory:;Foreign Keys=True");await c.OpenAsync();
        await using var db=new KnowledgeHubDbContext(new DbContextOptionsBuilder<KnowledgeHubDbContext>().UseSqlite(c).Options);
        var migrations=db.Database.GetMigrations().ToArray();var previous=migrations[^2];var migrator=db.GetService<IMigrator>();
        string[] oldColumns=[];string before="";List<string[]> oldIndexes=[];List<string[]> oldFks=[];string oldSchema="";
        if(upgrade)
        {
            await migrator.MigrateAsync(previous);await Seed(c);
            oldColumns=(await Rows(c,"PRAGMA table_info(evidence)")).Select(x=>x[1]).ToArray();
            before=JsonSerializer.Serialize(await Rows(c,"SELECT * FROM evidence ORDER BY id"));
            oldIndexes=await Rows(c,"SELECT name,sql FROM sqlite_master WHERE type='index' AND tbl_name='evidence' ORDER BY name");
            oldFks=await Rows(c,"PRAGMA foreign_key_list(evidence)");
            oldSchema=(await Rows(c,"SELECT sql FROM sqlite_master WHERE type='table' AND name='evidence'"))[0][0];
        }
        await migrator.MigrateAsync();
        if(!upgrade) await Seed(c);
        if(upgrade)
        {
            Assert.Equal(before,JsonSerializer.Serialize(await Rows(c,"SELECT "+string.Join(",",oldColumns)+" FROM evidence ORDER BY id")));
            var indexes=await Rows(c,"SELECT name,sql FROM sqlite_master WHERE type='index' AND tbl_name='evidence' ORDER BY name");
            foreach(var row in oldIndexes) Assert.Contains(indexes,x=>x.SequenceEqual(row));
            var fks=await Rows(c,"PRAGMA foreign_key_list(evidence)");
            foreach(var row in oldFks) Assert.Contains(fks,x=>x.Skip(2).SequenceEqual(row.Skip(2)));
            var schema=(await Rows(c,"SELECT sql FROM sqlite_master WHERE type='table' AND name='evidence'"))[0][0];
            foreach(Match match in Regex.Matches(oldSchema,"ck_evidence_[a-z_]+")) Assert.Contains(match.Value,schema);
        }
        var rows=await db.Evidence.AsNoTracking().OrderBy(e=>e.Id).ToArrayAsync();
        Assert.All(rows,e=>{Assert.Null(e.WithdrawnAt);Assert.Null(e.WithdrawnByUserId);Assert.Null(e.WithdrawnByDisplayName);Assert.Null(e.WithdrawalReason);Assert.Null(e.ReplacesHumanConfirmationId);});
        var keys=await Rows(c,"PRAGMA foreign_key_list(evidence)");
        Assert.Contains(keys,x=>x[2]=="users"&&x[3]=="withdrawn_by_user_id"&&x[6]=="RESTRICT");
        Assert.Contains(keys,x=>x[2]=="evidence"&&x[3]=="replaces_evidence_id"&&x[6]=="RESTRICT");
        foreach(var set in new[]{"withdrawn_at='2026-09-07'","replaces_evidence_id=id", "withdrawn_at='2026-09-07',withdrawn_by_user_id=1,withdrawn_by_display_name=' ',withdrawal_reason='reason'", "withdrawn_at='2026-09-07',withdrawn_by_user_id=1,withdrawn_by_display_name='user',withdrawal_reason=' '", "withdrawn_at='2026-09-07',withdrawn_by_user_id=1,withdrawn_by_display_name='user',withdrawal_reason='"+new string('a',1001)+"'"})
            await Assert.ThrowsAsync<SqliteException>(()=>Exec(c,"UPDATE evidence SET "+set+" WHERE id=100"));
        await Assert.ThrowsAsync<SqliteException>(()=>Exec(c,"UPDATE evidence SET replaces_evidence_id=100 WHERE id=101"));
        await Exec(c,"UPDATE evidence SET withdrawn_at='2026-09-07',withdrawn_by_user_id=1,withdrawn_by_display_name='user',withdrawal_reason='reason' WHERE id=100");
        await Exec(c,"UPDATE evidence SET replaces_evidence_id=100 WHERE id=102");
        await Assert.ThrowsAsync<SqliteException>(()=>Exec(c,"UPDATE evidence SET replaces_evidence_id=100 WHERE id=103"));
        await Assert.ThrowsAsync<SqliteException>(()=>Exec(c,"DELETE FROM evidence WHERE id=100"));
        // This user is used only by withdrawal, proving the new FK independently.
        await Exec(c,"INSERT INTO users(id,display_name,is_active,created_at,updated_at,version) VALUES(2,'withdrawer',1,'2026-09-07','2026-09-07',1)");
        await Exec(c,"UPDATE evidence SET withdrawn_by_user_id=2 WHERE id=100");
        await Assert.ThrowsAsync<SqliteException>(()=>Exec(c,"DELETE FROM users WHERE id=2"));
        await migrator.MigrateAsync(previous);
        Assert.DoesNotContain(await Rows(c,"PRAGMA table_info(evidence)"),x=>x[1]=="withdrawn_at");
        await migrator.MigrateAsync();
        Assert.Equal(4,await db.Evidence.CountAsync());
    }
    private static async Task Seed(SqliteConnection c)
    {
        await Exec(c,"INSERT INTO users(id,display_name,is_active,created_at,updated_at,version) VALUES(1,'old user',1,'2026-08-01','2026-08-02',3)");
        await Exec(c,"INSERT INTO knowledge_roles(id,name,is_active,created_at,updated_at,version) VALUES(1,'old role',1,'2026-08-01','2026-08-02',2)");
        foreach(var id in new[]{100,101,102,103})
        {
            var type=id==101?"Sql":"HumanConfirmation";
            await Exec(c,$$"""
                INSERT INTO evidence(id,evidence_type,subject_type,subject_id,subject_detail_key,source_title,source_reference,source_locator_json,summary,support_reason,confidence,provider_user_id,provider_knowledge_role_id,provider_employee_no,provider_name,provider_role,provider_team,provider_job_title,provider_external_key,provider_source,provider_note,provided_at,knowledge_document_revision_number_snapshot,created_at,updated_at,version)
                VALUES({{id}},'{{type}}','KnowledgeDocument',123,'Body','old title','old ref','{"confirmationStatement":"old statement"}','old summary','old support','Low',1,1,'old number','old name','old role snapshot','old team','old job','old key','old source','old note','2026-08-01T01:00:00+00:00',7,'2026-08-01T02:00:00+00:00','2026-08-02T02:00:00+00:00',11)
                """);
        }
    }
    private static async Task Exec(SqliteConnection c,string sql){await using var cmd=c.CreateCommand();cmd.CommandText=sql;await cmd.ExecuteNonQueryAsync();}
    private static async Task<List<string[]>> Rows(SqliteConnection c,string sql){await using var cmd=c.CreateCommand();cmd.CommandText=sql;await using var reader=await cmd.ExecuteReaderAsync();var rows=new List<string[]>();while(await reader.ReadAsync()) rows.Add(Enumerable.Range(0,reader.FieldCount).Select(i=>reader.IsDBNull(i)?"<NULL>":reader.GetValue(i).ToString()!).ToArray());return rows;}
}
