using CK.Core;
using CK.SqlServer;
using System.Threading.Tasks;

namespace CK.DB.Actor.ActorEMail;

/// <summary>
/// Holds mails (among them one is the primary email) for a user or a group.
/// By default, the UK_CK_tActorEMail_EMail unique constraint enforces the fact that no EMail can be shared among actors. 
/// </summary>
/// <remarks>
/// This CK.DB.Actor.ActorEMail package has been designed so that the same EMail address MAY
/// be associated to different actors: by deleting the UK_CK_tActorEMail_EMail unique constraint, it is
/// possible to support such scenarii where the same mail can be shared by different actors.
///  </remarks>
[SqlTable( "tActorEMail", Package = typeof( Package ) )]
[Versions( "1.0.0,2.0.1" )]
[SqlObjectItem( "transform:sUserDestroy, transform:sGroupDestroy, transform:vUser, transform:vGroup" )]
public abstract partial class ActorEMailTable : SqlTable
{
    /// <summary>
    /// Adds an email to a user or a group and/or sets whether it is the primary one.
    /// By default <paramref name="avoidAmbiguousEMail"/>> is true so that if the mail already exists for
    /// another user, nothing is done and the actor identifier that is bound to the existing EMail is returned.
    /// When avoidAmbiguousEMail is false, the behavior depends on the unicity of the EMail column: if the unique key exists (the default),
    /// a SqlException duplicate key error will be raised but if no unique key is defined (ie. the UK_CK_tActorEMail_EMail constraint has
    /// been dropped), the same email can be associated to different users.
    /// <para>
    /// The primary email is validated as soon as the actor has a validated email: requesting an email that is (or ends)
    /// not validated to be the primary one while the actor has another validated email is an error
    /// ("EMail.PrimaryMustBeValidated": validate it first, or with <paramref name="validate"/> true), and un-validating the
    /// primary email elects the most recently validated one, if any.
    /// </para>
    /// </summary>
    /// <param name="ctx">The call context to use.</param>
    /// <param name="actorId">The acting actor identifier.</param>
    /// <param name="userOrGroupId">The user or group identifier for which an email should be added or configured as the primary one.</param>
    /// <param name="email">The email.</param>
    /// <param name="isPrimary">True to set the email as the user or group's primary one.</param>
    /// <param name="validate">Optionaly sets the ValTime of the email: true to set it to sysUtcDateTime(), false to reset it to '0001-01-01'.</param>
    /// <param name="avoidAmbiguousEMail">False to skip EMail unicity check: always attempts to add the EMail to the actor.</param>
    /// <returns>
    /// The <paramref name="userOrGroupId"/> or, if <paramref name="avoidAmbiguousEMail"/> is true (the default), the identifier that
    /// is already associated to the mail.
    /// </returns>
    [SqlProcedure( "sActorEMailAdd" )]
    public abstract Task<int> AddEMailAsync( ISqlCallContext ctx, int actorId, int userOrGroupId, string email, bool isPrimary, bool? validate = null, bool avoidAmbiguousEMail = true );

    /// <summary>
    /// Removes an email from the user or group's emails (removing an unexisting email is silently ignored).
    /// When the removed email is the primary one, the most recently validated email becomes
    /// the new primary one.
    /// By default, this procedure allows the removal of the only actor's email.
    /// </summary>
    /// <param name="ctx">The call context to use.</param>
    /// <param name="actorId">The acting actor identifier.</param>
    /// <param name="userOrGroupId">The user or group identifier for which an email must be removed.</param>
    /// <param name="email">The email to remove.</param>
    /// <returns>The awaitable.</returns>
    [SqlProcedure( "sActorEMailRemove" )]
    public abstract Task RemoveEMailAsync( ISqlCallContext ctx, int actorId, int userOrGroupId, string email );

    /// <summary>
    /// Validates an email by uptating its ValTime to the current sysutdatetime.
    /// Validating a non existing email is silently ignored.
    /// If the current primary mail is not validated, this newly validated email becomes
    /// the primary one.
    /// </summary>
    /// <param name="ctx">The call context to use.</param>
    /// <param name="actorId">The acting actor identifier.</param>
    /// <param name="userOrGroupId">The user or group identifier for which the email is valid.</param>
    /// <param name="email">The email to valid.</param>
    /// <returns>The awaitable.</returns>
    [SqlProcedure( "sActorEMailValidate" )]
    public abstract Task ValidateEMailAsync( ISqlCallContext ctx, int actorId, int userOrGroupId, string email );

}
