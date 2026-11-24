Implements the `CK.tActorEMail` table: the email addresses of users and groups, each with its validation time, one of
them being the primary one. By default an email address belongs to a single actor.

`ActorEMailTable` adds, removes and validates the emails (`ActorEMailTable.AddEMailAsync`,
`ActorEMailTable.RemoveEMailAsync`, `ActorEMailTable.ValidateEMailAsync`). The `CK.vUser` and `CK.vGroup` views expose
the primary email and its validation time (`PrimaryEMail`, `PrimaryEMailValTime`).

The unicity of an email address is the `UK_CK_tActorEMail_EMail` unique constraint: the
CK.DB.Actor.ActorEMail.NonUnique package drops it.
