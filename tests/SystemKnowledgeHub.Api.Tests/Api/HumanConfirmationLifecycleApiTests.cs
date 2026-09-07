using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class HumanConfirmationLifecycleApiTests
{
    [Theory]
    [InlineData(AccessLevel.Editor)]
    [InlineData(AccessLevel.Administrator)]
    public async Task Withdraw_keeps_full_original_fact_and_audits_current_actor(AccessLevel role)
    {
        using var f = new BootstrapWebApplicationFactory(); using var creator = f.CreateAuthenticatedClient();
        var added = await Add(creator); var id = added.GetProperty("id").GetInt64();
        var before = await Read(f, id);
        using var actor = await Client(f, role);
        using var result = await Withdraw(actor, added, "  原确认结论存在业务口径错误  ");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        var response = await result.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(response.GetProperty("knowledgeStatusChanged").GetBoolean());
        Assert.False(response.TryGetProperty("withdrawnByUserId", out _));
        var after = await Read(f, id);
        Assert.NotEqual(after.ProviderUserId, after.WithdrawnByUserId);
        Assert.Equal("Lifecycle actor", after.WithdrawnByDisplayName);
        Assert.Equal("原确认结论存在业务口径错误", after.WithdrawalReason);
        Assert.Equal(before.Version + 1, after.Version);
        Assert.Equal(after.WithdrawnAt, after.UpdatedAt);
        Assert.InRange(after.WithdrawnAt!.Value, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow);
        after.WithdrawnAt = null; after.WithdrawnByUserId = null; after.WithdrawnByDisplayName = null;
        after.WithdrawalReason = null; after.Version = before.Version; after.UpdatedAt = before.UpdatedAt;
        Assert.Equal(JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
        var detail = await actor.GetFromJsonAsync<JsonElement>($"/api/evidence/{id}");
        Assert.Equal("Withdrawn", detail.GetProperty("humanConfirmationLifecycle").GetProperty("status").GetString());
        var list = await actor.GetFromJsonAsync<JsonElement>("/api/evidence?subjectType=BusinessFunction&subjectId=77");
        Assert.Contains(list.GetProperty("items").EnumerateArray(), e => e.GetProperty("id").GetInt64() == id);
        using var stale = await Withdraw(actor, added, "retry"); await Code(stale, 409, "conflict");
        using var repeat = await Withdraw(actor, response, "retry"); await Code(repeat, 422, "invalid_state");
        using var scope = f.Services.CreateScope();
        Assert.Equal("Inferred", (await scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>().BusinessFunctions.FindAsync(77L))!.KnowledgeStatus.ToString());
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)]
    public async Task C24_HC_type_rejection_precedes_fields_token_and_deleted_subject(bool withdrawn, bool deleted)
    {
        using var f = new BootstrapWebApplicationFactory(); using var c = f.CreateAuthenticatedClient();
        var a = await Add(c, "System", 12); var id = a.GetProperty("id").GetInt64();
        if (withdrawn) { using var w = await Withdraw(c, a, "withdraw"); Assert.Equal(HttpStatusCode.OK, w.StatusCode); }
        if (deleted)
        {
            using var scope = f.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var system = await db.Systems.FindAsync(12L); system!.IsDeleted = true;
            system.DeletedAt = DateTimeOffset.UtcNow; system.DeletedByUserId = await db.Users.Select(u => u.Id).FirstAsync(); system.DeletedByDisplayName = "test"; await db.SaveChangesAsync();
        }
        var before = JsonSerializer.Serialize(await Read(f,id));
        using var put = await c.PutAsJsonAsync($"/api/evidence/{id}", new { concurrencyToken = "invalid", sourceTitle = "", provider = (object?)null });
        await Code(put,422,"invalid_state"); Assert.Equal(before,JsonSerializer.Serialize(await Read(f,id)));
        if (deleted)
        {
            using var w = await Withdraw(c,a,"deleted subject correction"); Assert.Equal(HttpStatusCode.OK,w.StatusCode);
            using var create = await c.PostAsJsonAsync("/api/evidence/human-confirmations", Request("System",12,id)); await Code(create,422,"reference_invalid");
        }
    }

    [Theory]
    [InlineData(0,400)] [InlineData(1000,200)] [InlineData(1001,400)]
    public async Task Withdrawal_reason_boundaries(int size,int status)
    {
        using var f = new BootstrapWebApplicationFactory(); using var c = f.CreateAuthenticatedClient(); var a=await Add(c);
        using var result=await Withdraw(c,a,new string('x',size)); Assert.Equal(status,(int)result.StatusCode);
    }

    [Fact]
    public async Task Withdrawal_failures_are_closed_and_cannot_forge_audit()
    {
        using var f=new BootstrapWebApplicationFactory();using var c=f.CreateAuthenticatedClient();var a=await Add(c);var id=a.GetProperty("id").GetInt64();
        using var viewer=await Client(f,AccessLevel.Viewer);using var denied=await Withdraw(viewer,a,"reason");Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
        using var noCsrf=f.CreateAuthenticatedClientWithoutAntiforgery();using var csrf=await Withdraw(noCsrf,a,"reason");Assert.Equal(HttpStatusCode.Forbidden,csrf.StatusCode);
        using var spoof=await c.PostAsJsonAsync($"/api/evidence/human-confirmations/{id}/withdraw",new { reason="valid",concurrencyToken=a.GetProperty("concurrencyToken").GetString(),actor="spoof",withdrawnAt=DateTimeOffset.UtcNow});await Code(spoof,400,"validation_error");
        foreach(var badId in new[]{0L,9007199254740992L}) { using var bad=await c.PostAsJsonAsync($"/api/evidence/human-confirmations/{badId}/withdraw",new {reason="ok",concurrencyToken=a.GetProperty("concurrencyToken").GetString()});await Code(bad,400,"validation_error"); }
        using var missing=await c.PostAsJsonAsync("/api/evidence/human-confirmations/999999/withdraw",new {reason="ok",concurrencyToken=a.GetProperty("concurrencyToken").GetString()});await Code(missing,404,"not_found");
        using var invalid=await c.PostAsJsonAsync($"/api/evidence/human-confirmations/{id}/withdraw",new {reason="ok",concurrencyToken="bad"});await Code(invalid,400,"validation_error");
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var ordinaryId=await AddOrdinary(c); var ordinary=await db.Evidence.SingleAsync(e=>e.Id==ordinaryId);
        var detail=await c.GetFromJsonAsync<JsonElement>($"/api/evidence/{ordinary.Id}");using var wrong=await Withdraw(c,detail,"reason");await Code(wrong,422,"invalid_state");
        Assert.Null((await Read(f,id)).WithdrawnAt);
    }

    [Fact]
    public async Task Replacement_chain_is_unique_immutable_and_preserves_history()
    {
        using var f=new BootstrapWebApplicationFactory();using var c=f.CreateAuthenticatedClient();var a=await Add(c);var aid=a.GetProperty("id").GetInt64();
        using var active=await c.PostAsJsonAsync("/api/evidence/human-confirmations",Request("BusinessFunction",77,aid));await Code(active,422,"invalid_state");
        using var w=await Withdraw(c,a,"wrong conclusion");Assert.Equal(HttpStatusCode.OK,w.StatusCode);
        var old=JsonSerializer.Serialize(await Read(f,aid)); using var other=await Client(f,AccessLevel.Editor);
        var b=await Add(other,"BusinessFunction",77,aid);var bid=b.GetProperty("id").GetInt64();
        Assert.Equal(old,JsonSerializer.Serialize(await Read(f,aid)));
        using var w2=await Withdraw(other,b,"second correction");Assert.Equal(HttpStatusCode.OK,w2.StatusCode);
        using var branch=await c.PostAsJsonAsync("/api/evidence/human-confirmations",Request("BusinessFunction",77,aid));await Code(branch,409,"conflict");
        var last=await Add(c,"BusinessFunction",77,bid);
        var ad=await c.GetFromJsonAsync<JsonElement>($"/api/evidence/{aid}");Assert.Equal(bid,ad.GetProperty("humanConfirmationLifecycle").GetProperty("replacedByHumanConfirmationId").GetInt64());
        var bd=await c.GetFromJsonAsync<JsonElement>($"/api/evidence/{bid}");Assert.Equal(aid,bd.GetProperty("humanConfirmationLifecycle").GetProperty("replacesHumanConfirmationId").GetInt64());
        Assert.Equal(last.GetProperty("id").GetInt64(),bd.GetProperty("humanConfirmationLifecycle").GetProperty("replacedByHumanConfirmationId").GetInt64());
        using var mutate=await c.PutAsJsonAsync($"/api/evidence/{bid}",new {replacesHumanConfirmationId=aid});await Code(mutate,422,"invalid_state");
    }

    [Fact]
    public async Task Replacement_rejects_incompatible_references_and_detail_keys()
    {
        using var f=new BootstrapWebApplicationFactory();using var c=f.CreateAuthenticatedClient();var a=await Add(c);var aid=a.GetProperty("id").GetInt64();using var w=await Withdraw(c,a,"reason");
        foreach(var input in new object[]{Request("BusinessFunction",77,999999),Request("System",12,aid),Request("BusinessFunction",78,aid),Request("BusinessFunction",77,aid,"different")})
        {using var bad=await c.PostAsJsonAsync("/api/evidence/human-confirmations",input);await Code(bad,422,"reference_invalid");}
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();var ordinaryId=await AddOrdinary(c); var ordinary=await db.Evidence.SingleAsync(e=>e.Id==ordinaryId);
        using var wrong=await c.PostAsJsonAsync("/api/evidence/human-confirmations",Request("BusinessFunction",77,ordinary.Id));await Code(wrong,422,"reference_invalid");
    }

    [Theory]
    [InlineData("CodeReference")] [InlineData("Sql")] [InlineData("DatabaseSample")] [InlineData("DatabaseComment")]
    [InlineData("Api")] [InlineData("MqMessage")] [InlineData("ExistingDocument")]
    public async Task Ordinary_C24_keeps_correction(string type)
    {
        using var f=new BootstrapWebApplicationFactory();using var c=f.CreateAuthenticatedClient();
        using var create=await c.PostAsJsonAsync("/api/evidence",new {evidenceType=type,subject=new {type="BusinessFunction",id=77},sourceTitle="source",sourceReference="reference",supportReason="support",provider=new {displayName="A",roleOrIdentity="expert",occurredAt=DateTimeOffset.UtcNow}});
        Assert.Equal(HttpStatusCode.Created,create.StatusCode);var a=await create.Content.ReadFromJsonAsync<JsonElement>();
        using var update=await c.PutAsJsonAsync($"/api/evidence/{a.GetProperty("id").GetInt64()}",new {sourceTitle="corrected",sourceReference="reference2",supportReason="corrected support",provider=new {displayName="B",roleOrIdentity="new role",occurredAt=DateTimeOffset.UtcNow},actor=new {displayName="editor"},concurrencyToken=a.GetProperty("concurrencyToken").GetString()});
        Assert.Equal(HttpStatusCode.OK,update.StatusCode);var detail=await update.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal("B",detail.GetProperty("provider").GetProperty("displayName").GetString());Assert.Equal(JsonValueKind.Null,detail.GetProperty("humanConfirmationLifecycle").ValueKind);
    }

    [Fact]
    public async Task Document_replacement_and_active_coverage_use_exact_revision_without_status_mutation()
    {
        using var f=new BootstrapWebApplicationFactory();using var c=f.CreateAuthenticatedClient();
        using var created=await c.PostAsJsonAsync("/api/knowledge-documents",new {documentType="Requirement",title="HC revision",bodyMarkdown="one"});
        Assert.Equal(HttpStatusCode.Created,created.StatusCode);var doc=await created.Content.ReadFromJsonAsync<JsonElement>();var id=doc.GetProperty("id").GetInt64();
        var old=await Add(c,"KnowledgeDocument",id,revision:1);
        using var saved=await c.PutAsJsonAsync($"/api/knowledge-documents/{id}/content",new {title="HC revision",bodyMarkdown="two",concurrencyToken=doc.GetProperty("concurrencyToken").GetString()});Assert.Equal(HttpStatusCode.OK,saved.StatusCode);
        var current=await Add(c,"KnowledgeDocument",id,revision:2);
        async Task Coverage(string expected){var d=await c.GetFromJsonAsync<JsonElement>($"/api/knowledge-documents/{id}");Assert.Equal(expected,d.GetProperty("confirmationCoverage").GetProperty("state").GetString());Assert.Equal("Unknown",d.GetProperty("knowledgeStatus").GetString());}
        await Coverage("CurrentRevisionConfirmed");using var wc=await Withdraw(c,current,"current correction");Assert.Equal(HttpStatusCode.OK,wc.StatusCode);await Coverage("ChangedSinceConfirmation");
        using var wo=await Withdraw(c,old,"old correction");Assert.Equal(HttpStatusCode.OK,wo.StatusCode);await Coverage("NoConfirmation");
        using var bad=await c.PostAsJsonAsync("/api/evidence/human-confirmations",Request("KnowledgeDocument",id,old.GetProperty("id").GetInt64(),revision:2));await Code(bad,422,"reference_invalid");
        var replacement=await Add(c,"KnowledgeDocument",id,current.GetProperty("id").GetInt64(),revision:2);await Coverage("CurrentRevisionConfirmed");
        using var wr=await Withdraw(c,replacement,"again");Assert.Equal(HttpStatusCode.OK,wr.StatusCode);
        long legacyId;using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();var legacy=new SystemKnowledgeHub.Api.Features.Evidence.Domain.Evidence{SubjectType=EvidenceSubjectType.KnowledgeDocument,SubjectId=id,EvidenceType=EvidenceType.HumanConfirmation,SourceTitle="legacy",SourceReference="legacy",SupportReason="legacy",ProviderName="legacy",ProviderRole="legacy",ProvidedAt=DateTimeOffset.UtcNow,CreatedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow};db.Evidence.Add(legacy);await db.SaveChangesAsync();legacyId=legacy.Id;}
        await Coverage("LegacyConfirmationUnknown");var ld=await c.GetFromJsonAsync<JsonElement>($"/api/evidence/{legacyId}");using var wl=await Withdraw(c,ld,"legacy");Assert.Equal(HttpStatusCode.OK,wl.StatusCode);
        using var legacyBad=await c.PostAsJsonAsync("/api/evidence/human-confirmations",Request("KnowledgeDocument",id,legacyId,revision:2));await Code(legacyBad,422,"reference_invalid");await Coverage("NoConfirmation");
        using var stale=await c.PostAsJsonAsync("/api/evidence/human-confirmations",Request("KnowledgeDocument",id,revision:1));await Code(stale,409,"conflict");
    }

    [Fact]
    public async Task Effective_support_changes_while_relation_and_known_value_history_stays_protected()
    {
        using var f=new BootstrapWebApplicationFactory();using var c=f.CreateAuthenticatedClient();
        var system=await Add(c,"System",12);var function=await Add(c);var column=await Add(c,"DatabaseColumn",123,key:"KnownValues:HC-B01");
        using var created=await c.PostAsJsonAsync("/api/relationships",new {source=new {type="BusinessFunction",id=77},target=new {type="DatabaseObject",id=45},relationType="Reads"});Assert.Equal(HttpStatusCode.Created,created.StatusCode);
        var rid=(await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();var relation=await Add(c,"KnowledgeRelation",rid);
        long valueId;using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();var v=new SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain.ColumnKnownValue{DatabaseColumnId=123,ValueText="HC-B01",Meaning="history",CreatedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow};db.ColumnKnownValues.Add(v);await db.SaveChangesAsync();valueId=v.Id;}
        foreach(var hc in new[]{system,function,column,relation}){using var w=await Withdraw(c,hc,"remove current support only");Assert.Equal(HttpStatusCode.OK,w.StatusCode);}
        var view=await c.GetFromJsonAsync<JsonElement>("/api/systems/12/knowledge-view");Assert.Equal(0,view.GetProperty("overview").GetProperty("evidenceCount").GetInt32());
        var fd=await c.GetFromJsonAsync<JsonElement>("/api/business-functions/77");Assert.Contains(fd.GetProperty("evidence").EnumerateArray(),e=>e.GetProperty("isWithdrawn").GetBoolean());
        using var status=await c.PutAsJsonAsync("/api/knowledge-status",new {target=new {type="BusinessFunction",id=77},targetStatus="Confirmed",concurrencyToken=fd.GetProperty("concurrencyToken").GetString()});Assert.Equal(HttpStatusCode.UnprocessableEntity,status.StatusCode);
        using var removed=await c.DeleteAsync($"/api/relationships/{rid}");await Code(removed,422,"business_rule_violation");
        var cd=await c.GetFromJsonAsync<JsonElement>("/api/database-columns/123");Assert.Contains(cd.GetProperty("evidence").EnumerateArray(),e=>e.GetProperty("isWithdrawn").GetBoolean());
        using var known=await c.PostAsJsonAsync($"/api/database-columns/123/known-values/{valueId}/remove",new {confirmed=true,actor=new {displayName="editor",role="expert"},concurrencyToken=cd.GetProperty("concurrencyToken").GetString()});Assert.Equal(HttpStatusCode.UnprocessableEntity,known.StatusCode);
        using var finalScope=f.Services.CreateScope();var finalDb=finalScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();Assert.True(await finalDb.KnowledgeRelations.AnyAsync(r=>r.Id==rid));Assert.True(await finalDb.ColumnKnownValues.AnyAsync(v=>v.Id==valueId));
        // Each current projection sees ordinary + Active HC, while the withdrawn row remains historical.
        foreach(var target in new[]{("System",12L),("DatabaseColumn",123L),("KnowledgeRelation",rid)})
        {
            await Add(c,target.Item1,target.Item2);
            using var ordinary=await c.PostAsJsonAsync("/api/evidence",new {evidenceType="Sql",subject=new {type=target.Item1,id=target.Item2},sourceTitle="ordinary",sourceReference="ref",supportReason="support",provider=new {displayName="person",roleOrIdentity="expert",occurredAt=DateTimeOffset.UtcNow}});
            Assert.Equal(HttpStatusCode.Created,ordinary.StatusCode);
        }
        var mixedSystem=await c.GetFromJsonAsync<JsonElement>("/api/systems/12/knowledge-view");Assert.Equal(2,mixedSystem.GetProperty("overview").GetProperty("evidenceCount").GetInt32());
        var mixedFunction=await c.GetFromJsonAsync<JsonElement>("/api/business-functions/77");Assert.Equal(2,mixedFunction.GetProperty("relatedData").EnumerateArray().Single(r=>r.GetProperty("relationshipId").GetInt64()==rid).GetProperty("evidenceCount").GetInt32());
        var mixedDatabase=await c.GetFromJsonAsync<JsonElement>("/api/database-objects/45");Assert.Equal(2,mixedDatabase.GetProperty("columns").EnumerateArray().Single(r=>r.GetProperty("id").GetInt64()==123).GetProperty("evidenceCount").GetInt32());

    }

    [Fact]
    public async Task Inactive_actor_cannot_withdraw_and_canonical_service_rechecks_actor()
    {
        using var f=new BootstrapWebApplicationFactory();using var c=f.CreateAuthenticatedClient();var a=await Add(c);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var actor=new User{DisplayName="inactive",IsActive=false,AccessLevel=AccessLevel.Editor,CreatedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow};db.Users.Add(actor);await db.SaveChangesAsync();
        var service=scope.ServiceProvider.GetRequiredService<SystemKnowledgeHub.Api.Features.Evidence.Application.EvidenceService>();
        var result=await service.WithdrawHumanConfirmation(new SystemKnowledgeHub.Api.Features.Evidence.Application.Models.WithdrawHumanConfirmationCommand(a.GetProperty("id").GetInt64(),actor.Id,"reason",a.GetProperty("concurrencyToken").GetString()),CancellationToken.None);
        Assert.Equal(SystemKnowledgeHub.Api.Features.Evidence.Application.Models.EvidenceFailure.CurrentUserInactive,result.Failure);
        Assert.Null((await Read(f,a.GetProperty("id").GetInt64())).WithdrawnAt);
    }

    [Fact]
    public async Task Replacement_compares_normalized_historical_detail_keys()
    {
        using var f=new BootstrapWebApplicationFactory();using var c=f.CreateAuthenticatedClient();
        var old=await Add(c,key:"detail");using var w=await Withdraw(c,old,"normalize history");Assert.Equal(HttpStatusCode.OK,w.StatusCode);
        using(var scope=f.Services.CreateScope()){var db=scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();var row=await db.Evidence.FindAsync(old.GetProperty("id").GetInt64());row!.SubjectDetailKey="  detail  ";await db.SaveChangesAsync();}
        var replacement=await Add(c,replaces:old.GetProperty("id").GetInt64(),key:" detail ");Assert.Equal("detail",replacement.GetProperty("subjectDetailKey").GetString());
    }

    internal static async Task<long> AddOrdinary(HttpClient c)
    { using var r=await c.PostAsJsonAsync("/api/evidence",new {evidenceType="Sql",subject=new {type="BusinessFunction",id=77},sourceTitle="source",sourceReference="ref",supportReason="support",provider=new {displayName="person",roleOrIdentity="role",occurredAt=DateTimeOffset.UtcNow}});Assert.Equal(HttpStatusCode.Created,r.StatusCode);return (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64(); }
    internal static object Request(string type="BusinessFunction",long id=77,long? replaces=null,string? key=null,long? revision=null)=>new {subject=new {type,id},subjectDetailKey=key,subjectRevisionNumber=revision,replacesHumanConfirmationId=replaces,confirmationMethod="InSystem",confirmedAt="2026-09-07T01:00:00Z",confirmationStatement="确认规则正确",supportReason="业务确认",sourceNote="meeting"};
    internal static async Task<JsonElement> Add(HttpClient c,string type="BusinessFunction",long id=77,long? replaces=null,string? key=null,long? revision=null)
    {using var response=await c.PostAsJsonAsync("/api/evidence/human-confirmations",Request(type,id,replaces,key,revision));Assert.Equal(HttpStatusCode.Created,response.StatusCode);return await response.Content.ReadFromJsonAsync<JsonElement>();}
    internal static Task<HttpResponseMessage> Withdraw(HttpClient c,JsonElement a,string reason)=>c.PostAsJsonAsync($"/api/evidence/human-confirmations/{a.GetProperty("id").GetInt64()}/withdraw",new {reason,concurrencyToken=a.GetProperty("concurrencyToken").GetString()});
    internal static async Task Code(HttpResponseMessage r,int status,string code){Assert.Equal(status,(int)r.StatusCode);Assert.Equal(code,(await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());}
    internal static async Task<SystemKnowledgeHub.Api.Features.Evidence.Domain.Evidence> Read(BootstrapWebApplicationFactory f,long id){using var s=f.Services.CreateScope();return await s.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>().Evidence.AsNoTracking().SingleAsync(e=>e.Id==id);}
    private static async Task<HttpClient> Client(BootstrapWebApplicationFactory f,AccessLevel role){using var s=f.Services.CreateScope();var db=s.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();var u=new User{DisplayName="Lifecycle actor",AccessLevel=role,IsActive=true,CreatedAt=DateTimeOffset.UtcNow,UpdatedAt=DateTimeOffset.UtcNow};db.Users.Add(u);await db.SaveChangesAsync();return await f.CreateAuthenticatedClientAsync(u.Id);}
}
