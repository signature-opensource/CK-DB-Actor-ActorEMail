using CK.Core;
using CK.SqlServer;
using Shouldly;
using NUnit.Framework;
using System;
using Microsoft.Data.SqlClient;
using CK.Testing;
using static CK.Testing.MonitorTestHelper;


namespace CK.DB.Actor.ActorEMail.Tests;

[TestFixture]
public class ActorEMailTests
{
    [Test]
    public void adding_and_removing_one_mail_to_System()
    {
        var mails = SharedEngine.Map.StObjs.Obtain<ActorEMailTable>();
        using( var ctx = new SqlStandardCallContext( TestHelper.Monitor ) )
        {
            mails.Database.ExecuteScalar( "select PrimaryEMail from CK.vUser where UserId=1" )
                    .ShouldBe( DBNull.Value );

            mails.AddEMail( ctx, 1, 1, "god@heaven.com", false );
            mails.Database.ExecuteScalar( "select PrimaryEMail from CK.vUser where UserId=1" )
                    .ShouldBe( "god@heaven.com" );

            mails.RemoveEMail( ctx, 1, 1, "god@heaven.com" );
            mails.Database.ExecuteScalar( "select PrimaryEMail from CK.vUser where UserId=1" )
                    .ShouldBe( DBNull.Value );
        }
    }

    [Test]
    public void first_email_is_automatically_primary_but_the_first_valid_one_is_elected()
    {
        var group = SharedEngine.Map.StObjs.Obtain<GroupTable>();
        var mails = SharedEngine.Map.StObjs.Obtain<ActorEMailTable>();
        using( var ctx = new SqlStandardCallContext( TestHelper.Monitor ) )
        {
            var gId = group.CreateGroup( ctx, 1 );
            mails.AddEMail( ctx, 1, gId, "mail@address.com", false );
            mails.Database.ExecuteScalar( $"select PrimaryEMail from CK.vGroup where GroupId={gId}" )
                .ShouldBe( "mail@address.com" );

            mails.AddEMail( ctx, 1, gId, "Val-mail@address.com", false );
            mails.Database.ExecuteScalar( $"select PrimaryEMail from CK.vGroup where GroupId={gId}" )
                .ShouldBe( "mail@address.com" );

            mails.AddEMail( ctx, 1, gId, "bad-mail@address.com", false );
            mails.Database.ExecuteScalar( $"select PrimaryEMail from CK.vGroup where GroupId={gId}" )
                .ShouldBe( "mail@address.com" );


            mails.ValidateEMail( ctx, 1, gId, "Val-mail@address.com" );
            mails.Database.ExecuteScalar( $"select PrimaryEMail from CK.vGroup where GroupId={gId}" )
                .ShouldBe( "Val-mail@address.com" );

            group.DestroyGroup( ctx, 1, gId );
        }
    }

    [Test]
    public void when_removing_the_primary_email_another_one_is_elected_even_if_they_are_all_not_validated()
    {
        var user = SharedEngine.Map.StObjs.Obtain<UserTable>();
        var mails = SharedEngine.Map.StObjs.Obtain<ActorEMailTable>();
        using( var ctx = new SqlStandardCallContext( TestHelper.Monitor ) )
        {
            var uId = user.CreateUser( ctx, 1, Guid.NewGuid().ToString() );
            mails.AddEMail( ctx, 1, uId, "1@a.com", false );
            mails.AddEMail( ctx, 1, uId, "2@a.com", false );
            mails.AddEMail( ctx, 1, uId, "3@a.com", true );
            mails.AddEMail( ctx, 1, uId, "4@a.com", false );
            mails.Database.ExecuteScalar( $"select PrimaryEMail from CK.vUser where UserId={uId}" )
                .ShouldBe( "3@a.com" );

            mails.RemoveEMail( ctx, 1, uId, "3@a.com" );
            mails.Database.ExecuteScalar<string>( $"select PrimaryEMail from CK.vUser where UserId={uId}" )
                .ShouldMatch( m => m == "1@a.com" || m == "2@a.com" || m == "4@a.com" );
            user.DestroyUser( ctx, 1, uId );
        }
    }

    [Test]
    public void EMail_unicity_can_be_dropped_if_needed()
    {
        var user = SharedEngine.Map.StObjs.Obtain<UserTable>();
        var mails = SharedEngine.Map.StObjs.Obtain<ActorEMailTable>();
        using( var ctx = new SqlStandardCallContext( TestHelper.Monitor ) )
        {
            // We need a connection that stays Opened because we are playing with begin tran/rollback accross queries.
            ctx[mails.Database].PreOpen();

            var uniqueMail = $"{Guid.NewGuid():N}.sh@ared.com";
            var uId1 = user.CreateUser( ctx, 1, Guid.NewGuid().ToString() );
            mails.AddEMail( ctx, 1, uId1, "The-1-" + uniqueMail, isPrimary: false ).ShouldBe( uId1, "The 1 is the primary mail." );
            mails.AddEMail( ctx, 1, uId1, uniqueMail, false ).ShouldBe( uId1 );
            mails.Database.ExecuteScalar<string>( $"select PrimaryEMail from CK.vUser where UserId={uId1}" ).ShouldBe( "The-1-" + uniqueMail );
            mails.AddEMail( ctx, 1, uId1, uniqueMail, true ).ShouldBe( uId1, "Change the primary!" );
            mails.Database.ExecuteScalar<string>( $"select PrimaryEMail from CK.vUser where UserId={uId1}" ).ShouldBe( uniqueMail );
            mails.Database.ExecuteScalar<int>( $"select count(*) from CK.tActorEMail where ActorId={uId1}" ).ShouldBe( 2 );

            var uId2 = user.CreateUser( ctx, 1, Guid.NewGuid().ToString() );
            mails.AddEMail( ctx, 1, uId2, "The-2-" + uniqueMail, false ).ShouldBe( uId2, "The 2 is the primary mail." );
            mails.AddEMail( ctx, 1, uId2, uniqueMail, true ).ShouldBe( uId1, "Another user => the first user id is returned and nothing is done." );
            // Nothing changed for both user.
            mails.Database.ExecuteScalar<string>( $"select PrimaryEMail from CK.vUser where UserId={uId1}" ).ShouldBe( uniqueMail );
            mails.Database.ExecuteScalar<string>( $"select PrimaryEMail from CK.vUser where UserId={uId2}" ).ShouldBe( "The-2-" + uniqueMail );
            mails.Database.ExecuteScalar<int>( $"select count(*) from CK.tActorEMail where ActorId={uId2}" ).ShouldBe( 1 );

            // Calling with avoidAmbiguousEMail = false: behavior depends on UK_CK_tActorEMail_EMail constraint.
            bool isUnique = mails.Database.ExecuteScalar( "select object_id('CK.UK_CK_tActorEMail_EMail', 'UQ')" ) != DBNull.Value;
            if( isUnique )
            {
                TestHelper.Monitor.Info( "CK.UK_CK_tActorEMail_EMail constraint found: EMail cannot be shared among users." );
                // We cannot use the Database helpers here since the use a brand new SqlConnection each time.
                // We must use the SqlCallContext.
                Util.Invokable( () => mails.AddEMail( ctx, 1, uId2, uniqueMail, true, avoidAmbiguousEMail: false ) ).ShouldThrow<SqlDetailedException>();
                using( Util.CreateDisposableAction( () => ctx[mails.Database].ExecuteNonQuery( new SqlCommand( "rollback;" ) ) ) )
                {
                    ctx[mails.Database].ExecuteNonQuery( new SqlCommand( "begin tran; alter table CK.tActorEMail drop constraint UK_CK_tActorEMail_EMail;" ) );
                    TestWithoutUnicityConstraint( mails, ctx, uniqueMail, uId2 );
                }
            }
            else
            {
                TestHelper.Monitor.Info( "CK.UK_CK_tActorEMail_EMail constraint NOT found: EMail can be shared among users." );
                TestWithoutUnicityConstraint( mails, ctx, uniqueMail, uId2 );
                // We cannot test the unicity behavior here since applying the constraint will fail if current multiple emails exist.
            }

            static void TestWithoutUnicityConstraint( ActorEMailTable mails, SqlStandardCallContext ctx, string uniqueMail, int uId2 )
            {
                mails.AddEMail( ctx, 1, uId2, uniqueMail, true, avoidAmbiguousEMail: false ).ShouldBe( uId2 );
                ctx[mails.Database].ExecuteScalar( new SqlCommand( $"select count(*) from CK.tActorEMail where ActorId={uId2}" ) ).ShouldBe( 2 );
                ctx[mails.Database].ExecuteScalar( new SqlCommand( $"select count(*) from CK.tActorEMail where EMail='{uniqueMail}'" ) ).ShouldBe( 2 );
            }
        }
    }

    [Test]
    public void when_removing_the_primary_email_the_most_recently_validated_is_elected()
    {
        var user = SharedEngine.Map.StObjs.Obtain<UserTable>();
        var mails = SharedEngine.Map.StObjs.Obtain<ActorEMailTable>();
        using( var ctx = new SqlStandardCallContext( TestHelper.Monitor ) )
        {
            var uId = user.CreateUser( ctx, 1, Guid.NewGuid().ToString() );
            for( int i = 0; i < 10; i++ )
            {
                mails.AddEMail( ctx, 1, uId, $"fill{i}@a.com", false, true );
            }
            mails.AddEMail( ctx, 1, uId, "2@a.com", false );
            mails.AddEMail( ctx, 1, uId, "3@a.com", true, true );
            System.Threading.Thread.Sleep( 100 );
            mails.AddEMail( ctx, 1, uId, "4@a.com", false, true );
            mails.AddEMail( ctx, 1, uId, "5@a.com", false );
            mails.Database.ExecuteScalar( $"select PrimaryEMail from CK.vUser where UserId={uId}" ).ShouldBe( "3@a.com" );
            mails.RemoveEMail( ctx, 1, uId, "3@a.com" );
            mails.Database.ExecuteScalar( $"select PrimaryEMail from CK.vUser where UserId={uId}" ).ShouldBe( "4@a.com" );
            user.DestroyUser( ctx, 1, uId );
        }
    }

    [Test]
    public void an_email_that_is_not_validated_cannot_become_the_primary_one_while_a_validated_one_exists()
    {
        var user = SharedEngine.Map.StObjs.Obtain<UserTable>();
        var mails = SharedEngine.Map.StObjs.Obtain<ActorEMailTable>();
        using( var ctx = new SqlStandardCallContext( TestHelper.Monitor ) )
        {
            var uId = user.CreateUser( ctx, 1, Guid.NewGuid().ToString() );
            var prefix = Guid.NewGuid().ToString( "N" );
            mails.AddEMail( ctx, 1, uId, $"{prefix}.valid@a.com", isPrimary: true, validate: true );

            // A new address, or an existing one that is not validated, cannot be the primary one.
            Should.Throw<SqlDetailedException>( () => mails.AddEMail( ctx, 1, uId, $"{prefix}.new@a.com", isPrimary: true ) )
                  .InnerException!.Message.ShouldContain( "EMail.PrimaryMustBeValidated" );
            mails.AddEMail( ctx, 1, uId, $"{prefix}.other@a.com", isPrimary: false );
            Should.Throw<SqlDetailedException>( () => mails.AddEMail( ctx, 1, uId, $"{prefix}.other@a.com", isPrimary: true ) );
            mails.Database.ExecuteScalar( $"select PrimaryEMail from CK.vUser where UserId={uId}" ).ShouldBe( $"{prefix}.valid@a.com" );

            // Validated (in the same call, or before), it can.
            mails.AddEMail( ctx, 1, uId, $"{prefix}.new@a.com", isPrimary: true, validate: true );
            mails.Database.ExecuteScalar( $"select PrimaryEMail from CK.vUser where UserId={uId}" ).ShouldBe( $"{prefix}.new@a.com" );
            mails.ValidateEMail( ctx, 1, uId, $"{prefix}.other@a.com" );
            mails.AddEMail( ctx, 1, uId, $"{prefix}.other@a.com", isPrimary: true );
            mails.Database.ExecuteScalar( $"select PrimaryEMail from CK.vUser where UserId={uId}" ).ShouldBe( $"{prefix}.other@a.com" );

            user.DestroyUser( ctx, 1, uId );
        }
    }

    [Test]
    public void un_validating_the_primary_email_elects_the_most_recently_validated_one()
    {
        var user = SharedEngine.Map.StObjs.Obtain<UserTable>();
        var mails = SharedEngine.Map.StObjs.Obtain<ActorEMailTable>();
        using( var ctx = new SqlStandardCallContext( TestHelper.Monitor ) )
        {
            var uId = user.CreateUser( ctx, 1, Guid.NewGuid().ToString() );
            var prefix = Guid.NewGuid().ToString( "N" );
            mails.AddEMail( ctx, 1, uId, $"{prefix}.old@a.com", isPrimary: false, validate: true );
            System.Threading.Thread.Sleep( 100 );
            mails.AddEMail( ctx, 1, uId, $"{prefix}.recent@a.com", isPrimary: false, validate: true );
            mails.AddEMail( ctx, 1, uId, $"{prefix}.unvalidated@a.com", isPrimary: false );
            mails.Database.ExecuteScalar( $"select PrimaryEMail from CK.vUser where UserId={uId}" ).ShouldBe( $"{prefix}.old@a.com" );

            mails.AddEMail( ctx, 1, uId, $"{prefix}.old@a.com", isPrimary: false, validate: false );
            mails.Database.ExecuteScalar( $"select PrimaryEMail from CK.vUser where UserId={uId}" ).ShouldBe( $"{prefix}.recent@a.com" );

            // Without a validated address left, the primary one is not validated: it is the only possibility.
            mails.AddEMail( ctx, 1, uId, $"{prefix}.recent@a.com", isPrimary: false, validate: false );
            mails.Database.ExecuteScalar<string>( $"select PrimaryEMail from CK.vUser where UserId={uId}" ).ShouldNotBeNull();

            user.DestroyUser( ctx, 1, uId );
        }
    }

}
