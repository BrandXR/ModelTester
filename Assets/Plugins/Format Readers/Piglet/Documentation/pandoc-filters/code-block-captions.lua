-- Move each code block caption from above the code block to below the code block.
-- I wrote this filter because `pandoc-crossref` does not currently (as of Dec 2020)
-- provide any options for controlling the position of code block captions.
-- I really didn't like that the code block captions were appearing above the
-- code blocks while the figure captions were appearing below the figures.
--
-- This filter must be run *after* pandoc-crossref, e.g.:
-- `pandoc -o output.html --filter pandoc-crossref --lua-filter code-block-captions.lua input.md`.
--
-- I cobbled this code together by mimicking other pandoc filters I found
-- on the web. I really felt like I was working blind while writing this code,
-- because I didn't have any method to debug it when it wasn't working.

function Div(elem)
    if elem.t == "Div" and elem.classes[1] == "listing" and #elem.content == 2 then
       return pandoc.Div(pandoc.List({elem.content[2], elem.content[1]}), elem.attr)
    end
    return elem
end