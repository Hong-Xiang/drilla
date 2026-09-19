(module
  (func (export "add") (param $a i32) (param $b i32) (result i32)
    local.get $a
    local.get $b
    i32.add)

  (func (export "choose")
    (param $condition i32)
    (param $thenValue i32)
    (param $elseValue i32)
    (result i32)
    local.get $condition
    if (result i32)
      local.get $thenValue
    else
      local.get $elseValue
    end)

  (func (export "sum") (param $n i32) (result i32)
    (local $i i32)
    (local $sum i32)
    block $done
      loop $next
        local.get $i
        local.get $n
        i32.ge_s
        br_if $done

        local.get $sum
        local.get $i
        i32.add
        local.set $sum

        local.get $i
        i32.const 1
        i32.add
        local.set $i
        br $next
      end
    end
    local.get $sum))
