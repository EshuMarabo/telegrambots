create table public.bots (
    tg_bot_token text unique,
    bot_id serial,
    greeting text,
    primary key (bot_id)
);

create table public.users (
    user_id bigint,
    first_name text,
    last_name text,
    username text,
    primary key (user_id)
);

create table public.chats (
    chat_id bigint,
    bot_id int,
    user_id bigint,
    name text,
    chat_username text,
    user_group text,
    is_active bool default true not null,
    is_group bool default false,
    is_channel bool default false,
    primary key (chat_id,bot_id)
);

create table public.messages (
    db_timestamp timestamp default CURRENT_TIMESTAMP,
    client_timestamp timestamp,
    tg_timestamp timestamp,
    message_db_id bigserial,
    message_id bigint not null,
    chat_id bigint not null,
    user_id bigint not null,
    bot_id bigint,
    text varchar(4000),
    media_id text,
    media_group_id text,
    is_output bool default false,
    caption varchar(1000),
    in_reply_of bigint default 0,
    is_actual bool default true,
    deleted bool default false,
    pair_message_id bigint,
    pair_message_chat_id bigint,
    primary key (message_db_id),
    foreign key (bot_id) references public.bots (bot_id),
    foreign key (user_id) references public.users (user_id)
);

create table public.users_restrictions (
    restriction_id bigserial,
    user_id bigint,
    bot_id int,
    chat_id bigint,
    is_banned bool,
    is_fined bool,
    foreign key (user_id) references public.users (user_id),
    foreign key (bot_id) references public.bots (bot_id),
    primary key (restriction_id)
);

create table public.payments (
    payment_id serial,
    user_id bigint,
    payment_summ int not null,
    payment_operator text,
    payment_operator_code int not null ,
    payment_done bool default false,
    payment_text text,
    message_db_id bigint,
    primary key (user_id,payment_id)
);

create table public.phones (
    user_id bigint,
    phone bigint,
    primary key (user_id)
);


CREATE OR REPLACE FUNCTION get_bot_id(bot_token text) RETURNS int as
$$
    declare temp_bot_id int;
    begin
        temp_bot_id =(select public.bots.bot_id from public.bots where public.bots.tg_bot_token=bot_token);
        if (count(temp_bot_id))=0 then
            insert into public.bots (tg_bot_token) values (bot_token);
            temp_bot_id = get_bot_id(bot_token);
        end if;
        return temp_bot_id;
    end;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION add_user(id bigint,_user_name text default null,_first_name text default null,
    _last_name text default null) RETURNS void as
$$
    declare temp_user_id bigint;
    begin
        temp_user_id =(select public.users.user_id from public.users where public.users.user_id=id);
        if (count(temp_user_id))=0 then
            insert into public.users (user_id,username,first_name,last_name)
                values (id,_user_name,_first_name,_last_name);
        end if;
    end;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION add_chat(id bigint,bot_token text,_user_id bigint default null,
    _chat_username text default null,_name text default null, _is_channel bool default false,
    _is_group bool default false,_is_active bool default true)
    RETURNS void as
$$
    declare temp_chat_id bigint;
    begin
        insert into public.chats (chat_id,bot_id,user_id,name,chat_username,is_channel,is_group,is_active)
            values (id,get_bot_id(bot_token),_user_id,_name,_chat_username,_is_channel,_is_group,_is_active);
    end;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION ban_user(_user_id bigint,_chat_id bigint, token text) RETURNS void as
$$
    begin
        insert into  users_restrictions (user_id,bot_id,chat_id,is_banned) VALUES (_user_id,get_bot_id(token),_chat_id,true);
    end;
$$ LANGUAGE plpgsql;


CREATE OR REPLACE FUNCTION check_ban_user(_user_id bigint,_chat_id bigint, token text) RETURNS bool as
$$
    declare
        bans_number int;
    begin
        bans_number=(select count (public.users_restrictions.is_banned) from public.users_restrictions
        where public.users_restrictions.user_id=_user_id and public.users_restrictions.bot_id=get_bot_id(token)
            and public.users_restrictions.chat_id=_chat_id and public.users_restrictions.is_banned);
        if bans_number=0 then
            return false;
        else
            return true;
        end if;
    end;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION add_message(_tg_timestamp timestamp,_client_timestamp timestamp,_message_id bigint, _chat_id bigint, _user_id bigint,_in_reply_of bigint,
                                        bot_token text, _text text, _media_id text,_media_group_id text,_is_output bool, _caption text,_pair_chat_id bigint,_pair_message_id bigint) RETURNS void as
$$
    declare _bot_id int;
    begin
        _bot_id=get_bot_id(bot_token);
        insert into public.messages (tg_timestamp,client_timestamp,message_id, chat_id, user_id, bot_id, text, is_output, caption,
                                         in_reply_of, media_id,media_group_id,pair_message_chat_id,pair_message_id)
                values (_tg_timestamp,_client_timestamp,_message_id, _chat_id, _user_id, _bot_id, _text, _is_output, _caption,
                                         _in_reply_of, _media_id,_media_group_id,_pair_chat_id,_pair_message_id);
    end;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION get_pair_chat_id(_message_id bigint, _chat_id bigint, bot_token text) RETURNS bigint as
$$
    declare _bot_id int;
    declare result bigint;
    begin
        _bot_id=get_bot_id(bot_token);
        result =(select pair_message_chat_id from messages where chat_id=_chat_id and message_id = _message_id
                                       and bot_id=_bot_id and is_actual=true);
        return result;
    end;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION get_pair_message_id(_message_id bigint, _chat_id bigint, bot_token text) RETURNS bigint as
$$
    declare _bot_id int;
    declare result bigint;
    begin
        _bot_id=get_bot_id(bot_token);
        result =(select pair_message_id from messages where chat_id=_chat_id and message_id = _message_id
                                       and bot_id=_bot_id and is_actual=true);
        return result;
    end;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION message_info_act() RETURNS trigger as
$$
    declare
        double_value_number int;
    begin
        double_value_number = count((select message_db_id from public.messages
                where chat_id=new.chat_id and message_id=new.message_id and is_actual=true and bot_id=new.bot_id));
        /*if double_value_number=0 then
            return new;
        else*/
            if new.pair_message_chat_id is null and double_value_number>0 then
                new.pair_message_chat_id=(select pair_message_chat_id from public.messages
                    where chat_id=new.chat_id and message_id=new.message_id and is_actual=true and bot_id=new.bot_id);
            end if;

            if new.pair_message_id is null and double_value_number>0 then
                new.pair_message_id=(select pair_message_id from public.messages
                    where chat_id=new.chat_id and message_id=new.message_id and is_actual=true and bot_id=new.bot_id);
            end if;

            update public.messages set pair_message_chat_id=new.chat_id,pair_message_id=new.message_id
                where chat_id=new.pair_message_chat_id and message_id=new.pair_message_id and is_actual=true;

            update public.messages SET is_actual=false,pair_message_id=null,pair_message_chat_id=null
                where chat_id=new.chat_id and message_id=new.message_id and is_actual=true and bot_id=new.bot_id;
            return new;
       /* end if;*/

    end;
$$ LANGUAGE plpgsql;

CREATE OR REPLACE FUNCTION chat_info_act() RETURNS trigger as
$$
    declare
        chats_count int;
        current_is_active bool;
    begin
        current_is_active = (select chats.is_active from chats where chats.chat_id=new.chat_id and bot_id=new.bot_id);
        chats_count = count(current_is_active);
        if chats_count=0 then
            if new.is_active is null then
                new.is_active=true;
            end if;
            return new;
        else
            if new.is_active is null then
                new.is_active=current_is_active;
            end if;
            update chats SET is_group=new.is_group,is_channel=new.is_channel,name=new.name,
                             chat_username=new.chat_username,user_group=new.user_group,is_active=new.is_active
            where chats.chat_id=new.chat_id and bot_id=new.bot_id;
            return null;
        end if;
    end;
$$ LANGUAGE plpgsql;

CREATE TRIGGER message_info_actualisation before INSERT on public.messages FOR EACH ROW execute PROCEDURE message_info_act();

CREATE TRIGGER chat_info_actualisation before INSERT on public.chats FOR EACH ROW execute PROCEDURE chat_info_act();
