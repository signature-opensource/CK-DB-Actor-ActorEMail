-- SetupConfig: { }
--
-- Adds an email to a user or a group and/or sets whether it is the primary one.
-- Optionally sets the ValTime to sysutcdatetime() or '0001-01-01'.
-- By default @AvoidAmbiguousEMail is set to 1 so that if the mail already exists for another user,
-- nothing is done and the actor identifier that is bound to the EMail is returned in @UserOrGroupId parameter.
--
-- When @AvoidAmbiguousEMail is 0, the behavior depends on the unicity of the EMail column:
--   - if the unique key exists (teh default), a duplicate key error will be raised.
--   - if no unique key is defined (ie. the UK_CK_tActorEMail_EMail constraint has been dropped),
--     the same email can be associated to different users.
--
-- The primary email is validated as soon as the actor has a validated email:
--   - requesting an email that is (or ends) not validated to be the primary one while the actor has another
--     validated email is an error ('EMail.PrimaryMustBeValidated'): validate it first (or with @Validate = 1);
--   - un-validating (@Validate = 0) the primary email elects the most recently validated one, if any.
--
create procedure CK.sActorEMailAdd 
(
	@ActorId int,
	@UserOrGroupId int /*input*/output,
	@EMail nvarchar(255),
	@IsPrimary bit,
	@Validate bit = null,
    @AvoidAmbiguousEMail bit = 1
)
as
begin
    if @ActorId <= 0 throw 50000, 'Security.AnonymousNotAllowed', 1;
	if @EMail is null throw 50000, 'Argument.NullEMail', 1;
	set @EMail = rtrim(ltrim(@EMail));
	if len(@EMail) = 0 throw 50000, 'Argument.EmptyEMail', 1;
	if @IsPrimary is null throw 50000, 'Argument.NullIsPrimary', 1;

	--[beginsp]

    if @AvoidAmbiguousEMail = 1
    begin
        declare @AlreadyOtherActorId int;
        select @AlreadyOtherActorId = ActorId from CK.tActorEMail where EMail = @EMail;
        if @AlreadyOtherActorId is not null and @AlreadyOtherActorId <> @UserOrGroupId
        begin           
            --<AmbiguousEMailDetected />
            set @UserOrGroupId = @AlreadyOtherActorId;
        end
        else
        begin
            set @AvoidAmbiguousEMail = 0;
        end
    end

    if @AvoidAmbiguousEMail = 0
    begin
	    declare @ExistingPrimaryEMail nvarchar(255);
	    select @ExistingPrimaryEMail = EMail from CK.tActorEMail where ActorId = @UserOrGroupId and IsPrimary = 1
	    declare @NewPrimaryEMail nvarchar(255);
	    if @ExistingPrimaryEMail is null set @IsPrimary = 1;
	    else if @IsPrimary = 1
	    begin
		    -- An email that is not validated cannot become the primary one while a validated email exists.
		    declare @IsValidated bit = case when @Validate = 1 then 1
		                                    when @Validate = 0 then 0
		                                    else (select case when ValTime > '0001-01-01' then 1 else 0 end
		                                            from CK.tActorEMail where ActorId = @UserOrGroupId and EMail = @EMail)
		                               end;
		    if isnull( @IsValidated, 0 ) = 0
		       and exists( select 1 from CK.tActorEMail where ActorId = @UserOrGroupId and EMail <> @EMail and ValTime > '0001-01-01' )
		    begin
			    ;throw 50000, 'EMail.PrimaryMustBeValidated', 1;
		    end
	    end
	    if @IsPrimary = 1 set @NewPrimaryEMail = @EMail;
	    else set @NewPrimaryEMail = @ExistingPrimaryEMail;

	    --<PreEMailAdd revert />

	    -- Update or insert the @EMail.
	    merge CK.tActorEMail as target
		    using( select ActorId = @UserOrGroupId, EMail = @EMail ) 
		    as source on source.ActorId = target.ActorId and source.EMail = target.EMail
		    when matched then update set IsPrimary = @IsPrimary, 
									     ValTime = case when @Validate is null then target.ValTime 
													    when @Validate = 0 then '0001-01-01'
													    else sysutcdatetime() 
												    end
		    when not matched by target then insert( ActorId, EMail, IsPrimary, ValTime ) 
											    values( @UserOrGroupId, 
													    @EMail, 
													    @IsPrimary, 
													    case when @Validate is null or @Validate = 0 
														    then '0001-01-01'
														    else sysutcdatetime() 
													    end );
	    -- A little bit of defensive programming here: 
	    -- we always reset the IsPrimary bit based on @NewPrimaryEMail or elect a new one.
	    if @NewPrimaryEMail is null
	    begin
		    select top 1 @NewPrimaryEMail = EMail from CK.tActorEMail where ActorId = @UserOrGroupId order by ValTime desc;
	    end
	    -- The primary is validated when a validated email exists (the primary may just have been un-validated).
	    if not exists( select 1 from CK.tActorEMail where ActorId = @UserOrGroupId and EMail = @NewPrimaryEMail and ValTime > '0001-01-01' )
	    begin
		    select top 1 @NewPrimaryEMail = EMail from CK.tActorEMail
		        where ActorId = @UserOrGroupId and ValTime > '0001-01-01'
		        order by ValTime desc;
	    end

	    if @NewPrimaryEMail is not null
	    begin
		    update CK.tActorEMail 
			    set IsPrimary = case when EMail = @NewPrimaryEMail then 1 else 0 end 
			    where ActorId = @UserOrGroupId;
	    end

	    --<PostEMailAdd />
    end
    
	--[endsp]
end

