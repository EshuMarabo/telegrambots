alter table public.bots add COLUMN bot_user_id bigint;
alter table public.bots add COLUMN description text;
alter table public.bots add COLUMN type text;

update public.bots set type='FeedBack' where bots.type is null;

create table public.task_statuses (
    status_id serial,
    status_name text,
    primary key (status_id)
);


insert into public.task_statuses (status_name) VALUES ('created');
insert into public.task_statuses (status_name) VALUES ('formed');
insert into public.task_statuses (status_name) VALUES ('done');
insert into public.task_statuses (status_name) VALUES ('rejected');


alter table public.chats add foreign key (bot_id) references public.bots (bot_id);
alter table public.chats drop constraint chats_pkey;
alter table public.chats add primary key (chat_id,bot_id);

--alter table public.messages add FOREIGN KEY (user_id) references public.users (user_id);
alter table public.messages drop constraint messages_user_id_fkey;
alter table public.messages alter column user_id DROP NOT NULL ;



create table public.tasks (
    task_id serial,
    target_chat bigint,
    bot_id int,
    task_status int,
    task_type text,
    source_message_id bigint references public.messages (message_db_id) default null,
    source_chat bigint,
    action_time timestamp,
    primary key (task_id),
    foreign key (bot_id) references public.bots (bot_id),
    foreign key (target_chat,bot_id) references public.chats (chat_id,bot_id),
    foreign key (source_chat,bot_id) references public.chats (chat_id,bot_id),
    foreign key (task_status) references public.task_statuses (status_id)
);

create table public.reactions (
    task_id int references public.tasks(task_id),
    reaction_id serial,
    reaction_text text,
    reaction_counter int default 0,
    message_id bigint references messages (message_db_id) default null,
    primary key (reaction_id)
);

create table public.reactions_voters (
    vote_id serial,
    reaction_id int ,
    voted_user_id bigint,
    foreign key (reaction_id) references reactions (reaction_id),
    primary key (vote_id)
);

create table  public.buttons(
    id serial,
    callback_data text,
    message_id bigint,
    primary key (id),
    foreign key (message_id) references public.messages (message_db_id)
);

CREATE OR REPLACE FUNCTION get_reaction_id(text text,bot_token text,chat_id bigint) RETURNS int as
$$
    declare
        _task_id int;
        react_id int;
    begin
        _task_id = (select t.task_id from tasks t inner join bots b on t.bot_id = b.bot_id where b.tg_bot_token=bot_token and t.source_chat=chat_id and t.task_status=1);
        insert into public.reactions (reaction_text, task_id) values (text,_task_id) returning reaction_id into react_id;
        return react_id;
    end;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION count_reaction(id int, user_id bigint) RETURNS void as
$$
    declare votes_count int;
    begin
        votes_count = count((select count(*) from public.reactions_voters rv inner join public.reactions r on rv.reaction_id = r.reaction_id
            where rv.voted_user_id=user_id group by r.message_id having r.message_id=(select message_id from reactions where reaction_id=id)) );
        insert INTO public.reactions_voters (reaction_id, voted_user_id)  VALUES (id,user_id);
        if votes_count =0 then
            update public.reactions set reaction_counter=reaction_counter+1 where reaction_id=id;
        end if;
    end;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION get_counted_reactions(id int) RETURNS TABLE (_task_id_ int,
                                                                            _reaction_id_ int,
                                                                            _reaction_text_ text,
                                                                            _reaction_counter_ int) as
$$
    declare
        _task_id int;
    begin
        _task_id = (select task_id from public.reactions where reaction_id=id);
        RETURN QUERY
            select reactions.task_id,reaction_id,reaction_text,reaction_counter  from reactions where reactions.task_id=_task_id order by reaction_id;
    end;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION get_reactions_by_task(_task_id int) RETURNS TABLE (task_id int,
                                                                            _reaction_id int,
                                                                            _reaction_text text,
                                                                            _reaction_counter int) as
$$
    begin
        RETURN QUERY
            select reactions.task_id,reaction_id,reaction_text,reaction_counter  from reactions where reactions.task_id=_task_id;
    end;
$$ LANGUAGE plpgsql;

select add_task(227272610, -1001150699291,'1274761027:AAE-cWdKDmG0BQ5RusboZi1ECglRqU4n6As','PostCreation');

CREATE OR REPLACE FUNCTION get_bot_id(bot_token text) RETURNS int as
$$
    declare temp_bot_id int;
    begin
        temp_bot_id =(select public.bots.bot_id from public.bots where public.bots.tg_bot_token=bot_token);
        if count(temp_bot_id)=0 then
            insert into public.bots (tg_bot_token,bot_user_id ) values (bot_token, (regexp_matches(bot_token,'^(\d+):'))[1]::bigint);
            temp_bot_id = get_bot_id(bot_token);
        end if;
        return temp_bot_id;
    end;
$$ LANGUAGE plpgsql;

drop function add_message(_tg_timestamp timestamp,_client_timestamp timestamp,_message_id bigint, _chat_id bigint, _user_id bigint,_in_reply_of bigint,
                                        bot_token text, _text text, _media_id text,_media_group_id text,_is_output bool, _caption text,_pair_chat_id bigint,_pair_message_id bigint);

CREATE OR REPLACE FUNCTION add_task(source_chat_id bigint,target_chat_id bigint,bot_token text,_task_type text) RETURNS void as
$$
    begin
        update tasks set task_status=4 where task_status=1;
        insert into tasks (source_chat,target_chat, bot_id,task_type, task_status) values (source_chat_id, target_chat_id,get_bot_id(bot_token),_task_type, 1);
    end;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION update_task_time(source_chat_id bigint,bot_token text, _action_time timestamp ) RETURNS void as
$$
    begin
        update tasks set action_time = _action_time, task_status=2 where source_chat=source_chat_id and bot_id=get_bot_id(bot_token) and task_status=1;
    end;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION task_complited(source_chat_id bigint,target_chat_id bigint,bot_token text ) RETURNS void as
$$
    begin
        update tasks set task_status=3 where source_chat=source_chat_id and target_chat=target_chat_id and bot_id=get_bot_id(bot_token);
    end;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION get_active_tasks(bot_token text ) RETURNS TABLE (_source_chat bigint,
                                                                            _message_id bigint,
                                                                            _task_type text,
                                                                            _target_chat bigint,
                                                                            text varchar(4000),
                                                                            caption varchar(1000),
                                                                            media_id text,
                                                                            media_group_id text,
                                                                            task_id int,
                                                                            _action_time timestamp,
                                                                            chat_name text) as
$$
    begin
        RETURN QUERY
            select  distinct  source_chat,message_id,task_type,target_chat,messages.text,messages.caption,messages.media_id,messages.media_group_id,tasks.task_id, tasks.action_time,c.name from tasks inner join messages on tasks.source_message_id=messages.message_db_id inner join chats c on tasks.source_chat = c.chat_id
            where tasks.bot_id=get_bot_id(bot_token) and action_time<CURRENT_TIMESTAMP and task_status<3;
    end;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION get_future_tasks(bot_token text ) RETURNS TABLE (_source_chat bigint,
                                                                            _message_id bigint,
                                                                            _task_type text,
                                                                            _target_chat bigint,
                                                                            text varchar(4000),
                                                                            caption varchar(1000),
                                                                            media_id text,
                                                                            media_group_id text,
                                                                            task_id int,
                                                                            _action_time timestamp,
                                                                            chat_name text) as
$$
    begin
        RETURN QUERY
            select distinct source_chat,message_id,task_type,target_chat,messages.text,messages.caption,messages.media_id,messages.media_group_id,tasks.task_id, tasks.action_time,c.name from tasks inner join messages on tasks.source_message_id=messages.message_db_id inner join chats c on tasks.source_chat = c.chat_id
            where tasks.bot_id=get_bot_id(bot_token) and task_status<3;
    end;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION task_rejected(source_chat_id bigint,bot_token text ) RETURNS void as
$$
    begin
        update tasks set task_status=4 where source_chat=source_chat_id and bot_id=get_bot_id(bot_token) and task_status=1;
    end;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION task_rejected(_task_id bigint) RETURNS void as
$$
    begin
        update tasks set task_status=4 where task_id=_task_id;
    end;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION add_message_to_task() RETURNS trigger as
$$
    declare
        bot_uid bigint;
    begin
        bot_uid = (select bots.bot_user_id from bots where bots.bot_id=new.bot_id);
        if not bot_uid=new.user_id  then
            update public.tasks SET source_message_id=new.message_db_id
                where bot_id=new.bot_id and source_chat = new.chat_id and task_status=1 and source_message_id is null;
        end if;
        return null;
    end;
$$ LANGUAGE plpgsql;

CREATE TRIGGER add_message_to_tasks after INSERT on public.messages FOR EACH ROW execute PROCEDURE add_message_to_task();

CREATE OR REPLACE FUNCTION add_bot(bot_token text,bot_type text) RETURNS void as
$$
    declare temp_bot_id int;
    begin
        temp_bot_id = get_bot_id(bot_token);
        update public.bots set type=bot_type where tg_bot_token = bot_token;
    end;
$$ LANGUAGE plpgsql;