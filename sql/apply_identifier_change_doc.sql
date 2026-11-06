-- Запись новых s/n, MAC и PON на карточки при закрытии документа корректировки.
-- Вызывать из процедуры закрытия документа до смены статуса:
--   apply_identifier_change_doc(p_store_document_id);
-- Все строки документа проверяются вместе. Одна ошибка отменяет применение всего документа.
-- SN и MAC пишутся в два шага через временное значение, чтобы обмен номерами между карточками
-- проходил при уникальном индексе.

create or replace procedure apply_identifier_change_doc(p_store_document_id in number) is
  type t_row is record(
    store_document_id number,
    line_no           number,
    comm_id           number,
    serial_number     varchar2(100),
    mac_address       varchar2(100),
    pon_serial        varchar2(100),
    new_serial_number varchar2(100),
    new_mac_address   varchar2(100),
    new_pon_serial    varchar2(100),
    card_mac_address  varchar2(100),
    card_pon_serial   varchar2(100),
    filial_kod        number,
    verify_status_num number,
    verify_status_str varchar2(1000));

  type t_tab is table of t_row index by binary_integer;

  v_xml    clob;
  v_rc     sys_refcursor;
  v_row    t_row;
  v_ok     t_tab;
  v_errors varchar2(4000);
  v_errcnt pls_integer := 0;
  v_cnt    pls_integer := 0;
  v_own    number;
  v_cond   number;
  v_tech   number;

  function esc(p_value in varchar2) return varchar2 is
  begin
    return replace(replace(replace(replace(p_value, '&', '&amp;'), '<', '&lt;'), '>', '&gt;'), '"', '&quot;');
  end;

  procedure tag(p_name in varchar2, p_value in varchar2) is
  begin
    v_xml := v_xml || '<' || p_name || '>' || esc(p_value) || '</' || p_name || '>';
  end;

  function temp_value(p_comm_id in number) return varchar2 is
  begin
    return '#chg#' || to_char(p_comm_id);
  end;
begin
  if p_store_document_id is null then
    raise_application_error(-20001, 'Не указан документ');
  end if;

  v_xml := '<ArrayOfIdentifierChange>';
  for r in (select d.*
              from store_doc_identifier_change d
             where d.store_document_id = p_store_document_id
               and d.applied_date is null
             order by d.line_id) loop
    v_cnt := v_cnt + 1;
    v_xml := v_xml || '<IdentifierChange>';
    tag('store_document_id', to_char(p_store_document_id));
    tag('line_no', to_char(r.line_id));
    tag('serial_number', r.serial_number);
    tag('mac_address', r.mac_address);
    tag('pon_serial', r.pon_serial);
    tag('new_serial_number', r.new_serial_number);
    tag('new_mac_address', r.new_mac_address);
    tag('new_pon_serial', r.new_pon_serial);
    if nvl(r.parameter_change, 0) = 1 then
      tag('parameter_change', '1');
      if r.ownership_id is not null then
        tag('ownership_id', to_char(r.ownership_id));
      end if;
      if r.dev_condition_id is not null then
        tag('dev_condition_id', to_char(r.dev_condition_id));
      end if;
      if r.sosttech_kod is not null then
        tag('sosttech_kod', to_char(r.sosttech_kod));
      end if;
    end if;
    v_xml := v_xml || '</IdentifierChange>';
  end loop;
  v_xml := v_xml || '</ArrayOfIdentifierChange>';

  if v_cnt = 0 then
    return;
  end if;

  v_rc := verify_identifier_change(p_store_document_id, v_xml);
  loop
    fetch v_rc
      into v_row;
    exit when v_rc%notfound;

    if v_row.verify_status_num = 1 then
      v_ok(v_ok.count + 1) := v_row;
    else
      v_errcnt := v_errcnt + 1;
      if v_errcnt <= 20 then
        v_errors := substr(v_errors || chr(10) || v_row.serial_number || ': ' || v_row.verify_status_str, 1, 3800);
      end if;
    end if;
  end loop;
  close v_rc;

  if v_errcnt > 0 then
    raise_application_error(-20003,
                            'Смена идентификаторов не прошла проверку, строк с ошибкой: ' || v_errcnt || v_errors);
  end if;

  for i in 1 .. v_ok.count loop
    if v_ok(i).new_serial_number is not null then
      update eth_comm_atu c
         set c.numbdec = temp_value(c.comm_id)
       where c.comm_id = v_ok(i).comm_id
         and c.priznakdel = 0;
    end if;

    if v_ok(i).new_mac_address is not null then
      update store_device_param p
         set p.mac_address = temp_value(p.comm_id)
       where p.comm_id = v_ok(i).comm_id
         and p.priznakdel = 0;
    end if;
  end loop;

  for i in 1 .. v_ok.count loop
    if v_ok(i).new_serial_number is not null then
      update eth_comm_atu c
         set c.numbdec = v_ok(i).new_serial_number
       where c.comm_id = v_ok(i).comm_id
         and c.priznakdel = 0;
    end if;

    if v_ok(i).new_mac_address is not null then
      update store_device_param p
         set p.mac_address = v_ok(i).new_mac_address
       where p.comm_id = v_ok(i).comm_id
         and p.priznakdel = 0;
    end if;

    if v_ok(i).new_pon_serial is not null then
      update store_device_param p
         set p.pon_serial = v_ok(i).new_pon_serial
       where p.comm_id = v_ok(i).comm_id
         and p.priznakdel = 0;
    end if;

    select d.ownership_id, d.dev_condition_id, d.sosttech_kod
      into v_own, v_cond, v_tech
      from store_doc_identifier_change d
     where d.line_id = v_ok(i).line_no;

    if v_own is not null then
      update store_device_param p
         set p.ownership_id = v_own
       where p.comm_id = v_ok(i).comm_id
         and p.priznakdel = 0;
    end if;

    if v_cond is not null then
      update store_device_param p
         set p.dev_condition_id = v_cond
       where p.comm_id = v_ok(i).comm_id
         and p.priznakdel = 0;
    end if;

    if v_tech is not null then
      update store_device_param p
         set p.sosttech_kod = v_tech
       where p.comm_id = v_ok(i).comm_id
         and p.priznakdel = 0;
    end if;

    update store_doc_identifier_change d
       set d.applied_date      = sysdate,
           d.card_mac_address  = v_ok(i).card_mac_address,
           d.card_pon_serial   = v_ok(i).card_pon_serial,
           d.verify_status_str = 'Применено'
     where d.line_id = v_ok(i).line_no;
  end loop;
exception
  when others then
    if v_rc%isopen then
      close v_rc;
    end if;
    raise;
end;
/
