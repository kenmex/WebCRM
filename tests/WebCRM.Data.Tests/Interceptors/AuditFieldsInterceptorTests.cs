using Microsoft.EntityFrameworkCore;
using Shouldly;
using WebCRM.Data.Interceptors;

namespace WebCRM.Data.Tests.Interceptors;

public class AuditFieldsInterceptorTests
{
    private static readonly DateTimeOffset T1 = new(2026, 10, 9, 8, 30, 15, 789, TimeSpan.Zero);
    private static readonly DateTimeOffset T2 = new(2026, 10, 10, 9, 0, 0, TimeSpan.Zero);

    private readonly string _databaseName = Guid.NewGuid().ToString();
    private readonly FakeCurrentUser _user = new() { UserId = "user-1" };
    private readonly FakeTimeProvider _clock = new(T1);

    private AuditTestDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<AuditTestDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .AddInterceptors(new AuditFieldsInterceptor(_user, _clock))
            .Options);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Insert_sets_created_fields_to_the_second_and_leaves_updated_empty()
    {
        await using var db = CreateContext();
        var thing = new AuditedThing { Name = "A" };
        db.AuditedThings.Add(thing);

        await db.SaveChangesAsync(Ct);

        thing.CreatedAt.ShouldBe(new DateTime(2026, 10, 9, 8, 30, 15, DateTimeKind.Utc));
        thing.CreatedAt.Kind.ShouldBe(DateTimeKind.Utc);
        thing.CreatedBy.ShouldBe("user-1");
        thing.UpdatedAt.ShouldBeNull();
        thing.UpdatedBy.ShouldBeNull();
    }

    [Fact]
    public async Task Update_sets_updated_fields_and_cannot_change_created_fields()
    {
        int id;
        await using (var db = CreateContext())
        {
            var thing = new AuditedThing { Name = "A" };
            db.AuditedThings.Add(thing);
            await db.SaveChangesAsync(Ct);
            id = thing.Id;
        }

        _user.UserId = "user-2";
        _clock.UtcNow = T2;

        await using (var db = CreateContext())
        {
            var thing = await db.AuditedThings.SingleAsync(e => e.Id == id, Ct);
            thing.Name = "B";
            thing.CreatedBy = "someone-else"; // must be ignored
            thing.CreatedAt = T2.UtcDateTime;  // must be ignored
            await db.SaveChangesAsync(Ct);
        }

        await using (var db = CreateContext())
        {
            var saved = await db.AuditedThings.SingleAsync(e => e.Id == id, Ct);
            saved.Name.ShouldBe("B");
            saved.CreatedBy.ShouldBe("user-1");
            saved.CreatedAt.ShouldBe(new DateTime(2026, 10, 9, 8, 30, 15, DateTimeKind.Utc));
            saved.UpdatedBy.ShouldBe("user-2");
            saved.UpdatedAt.ShouldBe(T2.UtcDateTime);
        }
    }

    [Fact]
    public async Task Created_only_entity_gets_created_fields()
    {
        await using var db = CreateContext();
        var thing = new CreatedOnlyThing { Name = "token" };
        db.CreatedOnlyThings.Add(thing);

        await db.SaveChangesAsync(Ct);

        thing.CreatedBy.ShouldBe("user-1");
        thing.CreatedAt.ShouldBe(new DateTime(2026, 10, 9, 8, 30, 15, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Updated_only_entity_gets_updated_fields_on_insert()
    {
        await using var db = CreateContext();
        var thing = new UpdatedOnlyThing { Name = "settings" };
        db.UpdatedOnlyThings.Add(thing);

        await db.SaveChangesAsync(Ct);

        thing.UpdatedBy.ShouldBe("user-1");
        thing.UpdatedAt.ShouldBe(new DateTime(2026, 10, 9, 8, 30, 15, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Unaudited_entity_saves_without_asking_for_the_user()
    {
        _user.UserId = null;
        await using var db = CreateContext();
        db.PlainThings.Add(new PlainThing { Name = "plain" });

        await db.SaveChangesAsync(Ct);

        _user.Calls.ShouldBe(0);
        (await db.PlainThings.CountAsync(Ct)).ShouldBe(1);
    }

    [Fact]
    public async Task Audited_entity_without_a_signed_in_user_throws_and_saves_nothing()
    {
        _user.UserId = null;
        await using var db = CreateContext();
        db.AuditedThings.Add(new AuditedThing { Name = "A" });

        await Should.ThrowAsync<InvalidOperationException>(() => db.SaveChangesAsync(Ct));

        await using var check = CreateContext();
        (await check.AuditedThings.CountAsync(Ct)).ShouldBe(0);
    }

    [Fact]
    public void Synchronous_save_of_audited_entity_throws()
    {
        using var db = CreateContext();
        db.AuditedThings.Add(new AuditedThing { Name = "A" });

        Should.Throw<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void Synchronous_save_of_unaudited_entity_is_allowed()
    {
        using var db = CreateContext();
        db.PlainThings.Add(new PlainThing { Name = "plain" });

        db.SaveChanges().ShouldBe(1);
    }
}
