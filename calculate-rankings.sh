#!/usr/bin/env bash


#Required permissions
# GRANT SELECT (ID, exp, level, userid, job, updated_at), UPDATE (world_cpos, job_cpos, updated_at) on characters to 'rsvp-ranker'@'localhost';
# GRANT SELECT (ID, admin, ban_expire) on users to 'rsvp-ranker'@'localhost';
# GRANT ALL on rankings to 'rsvp-ranker'@'localhost';


QUERY=$(cat <<-END

create temporary table rankings (
	rank int not null,
	character_id int not null,
	rank_type int not null,
	index character_type(character_id, rank_type)
);

SET @row_number = 0;

insert into rankings select 
	(@row_number:=@row_number + 1) as rank, 
	c.ID,
	0
from characters c 
join users u on u.id = c.userid 
where u.admin = 0 and c.rankbanned = 0 and c.deleted_at is null and u.ban_expire <= NOW()
order by c.level desc, c.exp desc;

SET @row_number = 0;
insert into rankings select 
	(@row_number:=@row_number + 1) as rank, 
	c.ID,
	1
from characters c 
join users u on u.id = c.userid 
where u.admin = 0 and floor(c.job / 100) = 0 and c.rankbanned = 0 and c.deleted_at is null and u.ban_expire <= NOW()
order by c.level desc, c.exp desc;

SET @row_number = 0;
insert into rankings select 
	(@row_number:=@row_number + 1) as rank, 
	c.ID,
	1
from characters c 
join users u on u.id = c.userid 
where u.admin = 0 and floor(c.job / 100) = 1 and c.rankbanned = 0 and c.deleted_at is null and u.ban_expire <= NOW()
order by c.level desc, c.exp desc;


SET @row_number = 0;
insert into rankings select 
	(@row_number:=@row_number + 1) as rank, 
	c.ID,
	1
from characters c 
join users u on u.id = c.userid 
where u.admin = 0 and floor(c.job / 100) = 2 and c.rankbanned = 0 and c.deleted_at is null and u.ban_expire <= NOW()
order by c.level desc, c.exp desc;


SET @row_number = 0;
insert into rankings select 
	(@row_number:=@row_number + 1) as rank, 
	c.ID,
	1
from characters c 
join users u on u.id = c.userid 
where u.admin = 0 and floor(c.job / 100) = 3 and c.rankbanned = 0 and c.deleted_at is null and u.ban_expire <= NOW()
order by c.level desc, c.exp desc;


SET @row_number = 0;
insert into rankings select 
	(@row_number:=@row_number + 1) as rank, 
	c.ID,
	1
from characters c 
join users u on u.id = c.userid 
where u.admin = 0 and floor(c.job / 100) = 4 and c.rankbanned = 0 and c.deleted_at is null and u.ban_expire <= NOW()
order by c.level desc, c.exp desc;



update characters c 
left join rankings r on r.character_id = c.id and r.rank_type = 0
set c.world_cpos = COALESCE(r.rank, 0), c.updated_at = c.updated_at;

update characters c 
left join rankings r on r.character_id = c.id and r.rank_type = 1
set c.job_cpos = COALESCE(r.rank, 0), c.updated_at = c.updated_at;

END
)

echo $QUERY |  mysql -hlocalhost -ursvp-ranker -pYOURPASSWORD -D rsvp